using EduVi.Contracts.DTOs.CurriculumIngestion;
using EduVi.Contracts.Common;
using EduVi.Repositories.Interfaces;
using EduVi.Services.CurriculumIngestion;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace EduVi.WebAPI.BackgroundServices;

/// <summary>
/// BackgroundService consume kết quả curriculum ingestion từ Python worker qua RabbitMQ.
/// Không cần SignalR — admin polls GET /api/curriculum-ingestion/{documentCode} để xem status.
/// </summary>
public class CurriculumResultConsumerService : BackgroundService
{
    private readonly ILogger<CurriculumResultConsumerService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConnectionFactory _connectionFactory;

    private IConnection? _connection;
    private IChannel? _channel;

    private const string QueueName = "curriculum.ingestion.results";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CurriculumResultConsumerService(
        ILogger<CurriculumResultConsumerService> logger,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
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
        _logger.LogInformation("CurriculumResultConsumer starting...");

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

                        _logger.LogDebug("Received curriculum ingestion result: {Json}", json);

                        var progress = JsonSerializer.Deserialize<CurriculumIngestionProgressDto>(json, JsonOptions);

                        if (progress is not null && progress.DocumentId > 0)
                        {
                            await UpdateCurriculumDocumentAsync(progress);

                            _logger.LogInformation(
                                "Curriculum ingestion update for document {DocumentId}: {Status} ({Step}, {Progress}%)",
                                progress.DocumentId, progress.Status, progress.Step, progress.Progress);
                        }
                        else
                        {
                            _logger.LogWarning("Received malformed curriculum ingestion result, discarding.");
                        }

                        await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing curriculum ingestion result");
                        await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: CancellationToken.None);
                    }
                };

                await _channel.BasicConsumeAsync(
                    queue: QueueName,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                _logger.LogInformation("CurriculumResultConsumer started, listening on queue '{Queue}'", QueueName);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("CurriculumResultConsumer stopping...");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CurriculumResultConsumer connection failed. Retrying in 10 seconds...");

                if (_channel is not null) { try { await _channel.DisposeAsync(); } catch { } _channel = null; }
                if (_connection is not null) { try { await _connection.DisposeAsync(); } catch { } _connection = null; }

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
            await _channel.DisposeAsync();
        }
        if (_connection is not null)
        {
            await _connection.CloseAsync(cancellationToken);
            await _connection.DisposeAsync();
        }

        _logger.LogInformation("CurriculumResultConsumer stopped");
        await base.StopAsync(cancellationToken);
    }

    private async Task UpdateCurriculumDocumentAsync(CurriculumIngestionProgressDto progress)
    {
        using var scope = _scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var document = await unitOfWork.CurriculumDocumentRepository.GetByIdAsync(progress.DocumentId);
        if (document is null)
        {
            _logger.LogWarning("CurriculumDocument {DocumentId} not found for task {TaskId}", progress.DocumentId, progress.TaskId);
            return;
        }

        switch (progress.Status)
        {
            case "processing":
                document.Status = CurriculumDocumentStatusConstants.Processing;
                break;

            case "completed":
                // Distinguish ingestion-completed from deletion-completed by current status
                document.Status = document.Status == CurriculumDocumentStatusConstants.Deleting
                    ? CurriculumDocumentStatusConstants.Deleted
                    : CurriculumDocumentStatusConstants.Completed;
                if (progress.Stats is not null)
                    document.Stats = JsonSerializer.Serialize(progress.Stats);
                break;

            case "failed":
                document.Status = CurriculumDocumentStatusConstants.Failed;
                document.ErrorMessage = progress.Error;
                break;

            default:
                _logger.LogWarning("Unknown curriculum ingestion status '{Status}' for document {DocumentId}", progress.Status, progress.DocumentId);
                return;
        }

        unitOfWork.CurriculumDocumentRepository.Update(document);
        await unitOfWork.SaveChangesAsync();
    }
}
