using EduVi.Contracts.DTOs.Pipeline;
using EduVi.Repositories.Interfaces;
using EduVi.Services.Pipeline;
using EduVi.WebAPI.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;
using System.Text;
using System.Text.Json;

namespace EduVi.WebAPI.BackgroundServices;

/// <summary>
/// BackgroundService consume kết quả từ Python worker qua RabbitMQ
/// và push real-time xuống client qua SignalR.
/// Khi status == "completed" hoặc "failed": cập nhật Product trong DB.
/// </summary>
public class PipelineResultConsumerService : BackgroundService
{
    private readonly ILogger<PipelineResultConsumerService> _logger;
    private readonly IHubContext<PipelineHub> _hubContext;
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConnectionFactory _connectionFactory;

    private IConnection? _connection;
    private IChannel? _channel;

    private const string QueueName = "pipeline.results";

    public PipelineResultConsumerService(
        ILogger<PipelineResultConsumerService> logger,
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
        _logger.LogInformation("PipelineResultConsumer starting...");

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

                    _logger.LogDebug("Received pipeline result: {Json}", json);

                    var progress = JsonSerializer.Deserialize<PipelineProgressDto>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (progress is not null)
                    {
                        // Push to user's SignalR group
                        await _hubContext.Clients
                            .Group($"user_{progress.UserId}")
                            .SendAsync("PipelineProgress", progress, stoppingToken);

                        // Update status in Redis
                        var redisDb = _redis.GetDatabase();
                        await redisDb.StringSetAsync(
                            $"pipeline:status:{progress.TaskId}",
                            json,
                            TimeSpan.FromHours(1));

                        // Persist to DB when completed or failed
                        if (progress.Status is "completed" or "failed" && progress.ProductId > 0)
                        {
                            await UpdateProductInDatabaseAsync(progress);
                        }

                        _logger.LogInformation(
                            "Pushed progress for task {TaskId} (Product {ProductId}) to user {UserId}: {Status} ({Progress}%)",
                            progress.TaskId, progress.ProductId, progress.UserId, progress.Status, progress.Progress);
                    }

                    await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing pipeline result message");
                    await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
                }
            };

            await _channel.BasicConsumeAsync(
                queue: QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

                        _logger.LogInformation("PipelineResultConsumer started, listening on queue '{Queue}'", QueueName);

                        // Keep the service running until cancelled or disconnected
                        await Task.Delay(Timeout.Infinite, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("PipelineResultConsumer stopping...");
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "PipelineResultConsumer connection failed. Retrying in 10 seconds...");

                        // Clean up before retry
                        if (_channel is not null) { try { _channel.Dispose(); } catch { } _channel = null; }
                        if (_connection is not null) { try { _connection.Dispose(); } catch { } _connection = null; }

                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                        }
                        catch (OperationCanceledException) { break; }
                    }
                }
            }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            _channel.Dispose();
        }
        if (_connection is not null)
        {
            await _connection.CloseAsync(cancellationToken);
            _connection.Dispose();
        }

        _logger.LogInformation("PipelineResultConsumer stopped");
        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Cập nhật Product status và lưu evaluation result vào DB
    /// Dùng IServiceScopeFactory vì BackgroundService là singleton, DbContext là scoped
    /// </summary>
    private async Task UpdateProductInDatabaseAsync(PipelineProgressDto progress)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var product = await unitOfWork.PipelineRepository
                .GetProductByIdAsync(progress.ProductId);

            if (product is null)
            {
                _logger.LogWarning("Product {ProductId} not found for task {TaskId}", progress.ProductId, progress.TaskId);
                return;
            }

            if (progress.Status == "completed")
            {
                product.Status = ProductStatusConstants.Evaluated;
                product.EvaluationResult = progress.Result is not null
                    ? JsonSerializer.Serialize(progress.Result)
                    : null;
                product.EvaluatedAt = DateTime.UtcNow;

                _logger.LogInformation("Product {ProductId} marked as EVALUATED", product.ProductId);
            }
            else if (progress.Status == "failed")
            {
                product.Status = ProductStatusConstants.Failed;
                product.EvaluationResult = JsonSerializer.Serialize(new
                {
                    error = progress.Error,
                    failedAt = DateTime.UtcNow.ToString("o")
                });
                product.EvaluatedAt = DateTime.UtcNow;

                _logger.LogWarning("Product {ProductId} marked as FAILED: {Error}", product.ProductId, progress.Error);
            }

            unitOfWork.PipelineRepository.UpdateProduct(product);
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Product {ProductId} in database for task {TaskId}",
                progress.ProductId, progress.TaskId);
        }
    }
}
