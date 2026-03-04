using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace EduVi.Services.Pipeline;

public class RabbitMqPublisherService : IRabbitMqPublisherService, IAsyncDisposable
{
    private readonly ILogger<RabbitMqPublisherService> _logger;
    private readonly ConnectionFactory _connectionFactory;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private const string LessonAnalysisQueue = "lesson.analysis.requests";
    private const string SlideGenerationQueue = "slide.generation.requests";

    public RabbitMqPublisherService(IConfiguration configuration, ILogger<RabbitMqPublisherService> logger)
    {
        _logger = logger;

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

    private async Task EnsureConnectedAsync()
    {
        if (_connection is { IsOpen: true } && _channel is { IsOpen: true })
            return;

        _connection = await _connectionFactory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        await _channel.QueueDeclareAsync(
            queue: LessonAnalysisQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        await _channel.QueueDeclareAsync(
            queue: SlideGenerationQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _logger.LogInformation("RabbitMQ publisher connected and queues declared: '{LessonQueue}', '{SlideQueue}'",
            LessonAnalysisQueue, SlideGenerationQueue);
    }

    public async Task PublishLessonAnalysisTaskAsync(Guid taskId, string userId, int productId, string gcsUri, string subjectCode, string gradeCode, string lessonCode)
    {
        await _semaphore.WaitAsync();
        try
        {
            await EnsureConnectedAsync();

            var message = new
            {
                taskId = taskId.ToString(),
                userId,
                productId,
                gcsUri,
                subjectCode,
                gradeCode,
                lessonCode
            };

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json"
            };

            await _channel!.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: LessonAnalysisQueue,
                mandatory: false,
                basicProperties: properties,
                body: body);

            _logger.LogInformation("Published lesson analysis task {TaskId} for user {UserId}", taskId, userId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task PublishSlideGenerationTaskAsync(Guid taskId, string userId, int productId, object evaluationResult, string lessonPlanText, object textbookSections, string slideRange)
    {
        await _semaphore.WaitAsync();
        try
        {
            await EnsureConnectedAsync();

            var message = new
            {
                taskId = taskId.ToString(),
                userId,
                productId,
                evaluationResult,
                lessonPlanText,
                textbookSections,
                preferences = new
                {
                    slideRange
                }
            };

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json"
            };

            await _channel!.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: SlideGenerationQueue,
                mandatory: false,
                basicProperties: properties,
                body: body);

            _logger.LogInformation("Published slide generation task {TaskId} for user {UserId}", taskId, userId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync();
            _channel.Dispose();
        }
        if (_connection is not null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
