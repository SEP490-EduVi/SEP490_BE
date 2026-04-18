using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Games.Response;
using EduVi.Repositories.Interfaces;
using EduVi.WebAPI.Hubs;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;
using System.Text;
using System.Text.Json;

namespace EduVi.WebAPI.BackgroundServices;

/// <summary>
/// BackgroundService consume kết quả game generation từ AI worker qua RabbitMQ
/// và lưu trạng thái vào Redis + ProductGames.
/// </summary>
public class GameResultConsumerService : BackgroundService
{
    private readonly ILogger<GameResultConsumerService> _logger;
    private readonly IHubContext<PipelineHub> _hubContext;
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConnectionFactory _connectionFactory;

    private IConnection? _connection;
    private IChannel? _channel;

    private const string QueueName = "game.quiz.results";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GameResultConsumerService(
        ILogger<GameResultConsumerService> logger,
        IHubContext<PipelineHub> hubContext,
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _hubContext = hubContext;
        _redis = redis;
        _scopeFactory = scopeFactory;

        var rabbitConfig = configuration.GetSection("RabbitMQ");
        _connectionFactory = new ConnectionFactory
        {
            HostName = rabbitConfig["HostName"] ?? "localhost",
            Port = int.Parse(rabbitConfig["Port"] ?? "5672"),
            UserName = rabbitConfig["UserName"] ?? "guest",
            Password = rabbitConfig["Password"] ?? "guest",
            VirtualHost = rabbitConfig["VirtualHost"] ?? "/"
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GameResultConsumer starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _connection = await _connectionFactory.CreateConnectionAsync(stoppingToken);
                _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await _channel.QueueDeclareAsync(
                    queue: QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: stoppingToken);

                await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.ReceivedAsync += async (_, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var json = Encoding.UTF8.GetString(body);

                        if (!TryDeserializeGameProgress(json, out var progress, out var parseError))
                        {
                            _logger.LogWarning("Dropping invalid game result message. Reason: {Reason}. Payload: {Payload}", parseError, json);
                            // Ack invalid payload to avoid poison-message redelivery loops.
                            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
                            return;
                        }

                        var redisDb = _redis.GetDatabase();
                        await redisDb.StringSetAsync(
                            $"game:status:{progress!.TaskId}",
                            JsonSerializer.Serialize(progress),
                            TimeSpan.FromHours(1));

                        await UpdateProductGameInDatabaseAsync(progress);

                        if (!string.IsNullOrWhiteSpace(progress.UserId))
                        {
                            await _hubContext.Clients
                                .Group($"user_{progress.UserId}")
                                .SendAsync("GameProgress", progress, stoppingToken);
                        }

                        await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing game result message");
                        await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: CancellationToken.None);
                    }
                };

                await _channel.BasicConsumeAsync(queue: QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GameResultConsumer connection failed. Retrying in 5s...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            await _channel.DisposeAsync();
        }
        if (_connection is not null)
        {
            await _connection.CloseAsync(cancellationToken);
            await _connection.DisposeAsync();
        }
        await base.StopAsync(cancellationToken);
    }

    private static bool TryDeserializeGameProgress(string json, out GameProgressDto? progress, out string? error)
    {
        progress = null;
        error = null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "Payload root must be a JSON object";
                return false;
            }

            if (!TryGetGuid(root, "taskId", out var taskId) && !TryGetGuid(root, "task_id", out taskId))
            {
                error = "taskId is missing or is not a valid GUID";
                return false;
            }

            progress = new GameProgressDto
            {
                TaskId = taskId,
                UserId = GetString(root, "userId") ?? GetString(root, "user_id") ?? string.Empty,
                TemplateId = GetString(root, "templateId") ?? GetString(root, "template_id") ?? string.Empty,
                Status = GetString(root, "status") ?? string.Empty,
                Step = GetString(root, "step") ?? string.Empty,
                Progress = GetInt(root, "progress") ?? 0,
                Detail = GetString(root, "detail"),
                Result = CloneJsonValue(root, "result"),
                Error = GetString(root, "error")
            };

            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private async Task UpdateProductGameInDatabaseAsync(GameProgressDto progress)
    {
        if (progress.TaskId == Guid.Empty)
            return;

        if (progress.Status is not "completed" and not "failed")
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var productGame = await unitOfWork.GameRepository.GetProductGameByTaskIdAsync(progress.TaskId);
            if (productGame is null)
            {
                _logger.LogWarning("ProductGame not found for task {TaskId}", progress.TaskId);
                return;
            }

            if (productGame.Status == GameStatusConstants.Deleted)
            {
                _logger.LogInformation("Skip persistence for deleted game {GameCode}", productGame.ProductGameCode);
                return;
            }

            productGame.UpdatedAt = DateTime.UtcNow;

            if (progress.Status == "completed")
            {
                productGame.Status = GameStatusConstants.Completed;
                productGame.CompletedAt = DateTime.UtcNow;
                productGame.ErrorMessage = null;
                productGame.ResultJson = progress.Result is not null
                    ? JsonSerializer.Serialize(progress.Result)
                    : null;
            }
            else
            {
                productGame.Status = GameStatusConstants.Failed;
                productGame.ErrorMessage = progress.Error;
            }

            unitOfWork.GameRepository.UpdateProductGame(productGame);
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update game persistence for task {TaskId}", progress.TaskId);
        }
    }

    private static bool TryGetGuid(JsonElement root, string propertyName, out Guid value)
    {
        value = Guid.Empty;

        if (!root.TryGetProperty(propertyName, out var property))
            return false;

        if (property.ValueKind == JsonValueKind.String)
            return Guid.TryParse(property.GetString(), out value);

        return false;
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.String)
            return property.GetString();

        if (property.ValueKind == JsonValueKind.Null)
            return null;

        return property.GetRawText();
    }

    private static int? GetInt(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var numberValue))
            return numberValue;

        if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out var parsedNumberValue))
            return parsedNumberValue;

        return null;
    }

    private static object? CloneJsonValue(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
            return null;

        return property.Clone();
    }
}
