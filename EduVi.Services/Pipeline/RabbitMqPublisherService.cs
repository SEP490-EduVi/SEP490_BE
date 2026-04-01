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
    private const string VideoGenerationQueue = "video.generation.requests";
    private const string CurriculumIngestionQueue = "curriculum.ingestion.requests";
    private const string GameGenerationQueue = "game.quiz.requests";

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

        await _channel.QueueDeclareAsync(
            queue: VideoGenerationQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        await _channel.QueueDeclareAsync(
            queue: CurriculumIngestionQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        await _channel.QueueDeclareAsync(
            queue: GameGenerationQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _logger.LogInformation("RabbitMQ publisher connected and queues declared: '{LessonQueue}', '{SlideQueue}', '{VideoQueue}', '{CurriculumQueue}', '{GameQueue}'",
            LessonAnalysisQueue, SlideGenerationQueue, VideoGenerationQueue, CurriculumIngestionQueue, GameGenerationQueue);
    }

    public async Task PublishLessonAnalysisTaskAsync(Guid taskId, string userId, int productId, string gcsUri, string subjectCode, string gradeCode, string lessonCode, int? curriculumYear)
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
                lessonCode,
                curriculumYear
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

    public async Task PublishSlideGenerationTaskAsync(Guid taskId, string userId, int productId, object evaluationResult, string lessonPlanText, string slideRange)
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

    public async Task PublishVideoGenerationTaskAsync(Guid taskId, string productVideoCode, string userId, int productId, string productCode, string slideEditedDocumentUrl)
    {
        await _semaphore.WaitAsync();
        try
        {
            await EnsureConnectedAsync();

            var message = new
            {
                taskId = taskId.ToString(),
                requestId = productVideoCode,
                userId,
                productId,
                productCode,
                slideEditedDocumentUrl
            };

            var bodyJson = JsonSerializer.Serialize(message);
            if (string.IsNullOrWhiteSpace(bodyJson))
                throw new InvalidOperationException($"Serialized RabbitMQ video message is empty. TaskId={taskId}, RequestId={productVideoCode}, ProductId={productId}");

            var body = Encoding.UTF8.GetBytes(bodyJson);
            if (body.Length <= 0)
                throw new InvalidOperationException($"Serialized RabbitMQ video message has zero length body. TaskId={taskId}, RequestId={productVideoCode}, ProductId={productId}");

            _logger.LogInformation(
                "Video publish payload prepared. TaskId={TaskId}, RequestId={RequestId}, ProductId={ProductId}, BodyLength={BodyLength}",
                taskId,
                productVideoCode,
                productId,
                body.Length);
            _logger.LogInformation(
                "Video publish payload JSON. TaskId={TaskId}, RequestId={RequestId}, ProductId={ProductId}, Body={BodyJson}",
                taskId,
                productVideoCode,
                productId,
                bodyJson);

            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                Persistent = true
            };

            await _channel!.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: VideoGenerationQueue,
                mandatory: false,
                basicProperties: properties,
                body: body);

            _logger.LogInformation(
                "Published video generation task {TaskId} for user {UserId}, product {ProductId}, request {RequestId}",
                taskId,
                userId,
                productId,
                productVideoCode);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task PublishCurriculumIngestionTaskAsync(Guid taskId, int documentId, string gcsUri, string subjectCode, string educationLevel, int curriculumYear)
    {
        await _semaphore.WaitAsync();
        try
        {
            await EnsureConnectedAsync();

            var message = new
            {
                taskId = taskId.ToString(),
                documentId,
                gcsUri,
                subjectCode,
                educationLevel,
                curriculumYear
            };

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json"
            };

            await _channel!.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: CurriculumIngestionQueue,
                mandatory: false,
                basicProperties: properties,
                body: body);

            _logger.LogInformation("Published curriculum ingestion task {TaskId} for document {DocumentId}", taskId, documentId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task PublishGameGenerationTaskAsync(Guid taskId, object message)
    {
        await _semaphore.WaitAsync();
        try
        {
            await EnsureConnectedAsync();

            var bodyJson = JsonSerializer.Serialize(message);
            if (string.IsNullOrWhiteSpace(bodyJson))
                throw new InvalidOperationException($"Serialized RabbitMQ game message is empty. TaskId={taskId}");

            var body = Encoding.UTF8.GetBytes(bodyJson);
            if (body.Length <= 0)
                throw new InvalidOperationException($"Serialized RabbitMQ game message has zero length body. TaskId={taskId}");

            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                Persistent = true
            };

            await _channel!.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: GameGenerationQueue,
                mandatory: false,
                basicProperties: properties,
                body: body);

            _logger.LogInformation("Published game generation task {TaskId}", taskId);
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
            await _channel.DisposeAsync();
        }
        if (_connection is not null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
