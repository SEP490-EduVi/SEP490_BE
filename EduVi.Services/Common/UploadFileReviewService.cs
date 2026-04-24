using EduVi.Contracts.DTOs.AIReview;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace EduVi.Services.Common;

public class UploadFileReviewService : IUploadFileReviewService, IAsyncDisposable
{
    private readonly ILogger<UploadFileReviewService> _logger;
    private readonly ConnectionFactory _connectionFactory;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private static readonly JsonSerializerOptions CamelCaseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private const string ReviewRequestQueue = "file.review.requests";

    public UploadFileReviewService(IConfiguration configuration, ILogger<UploadFileReviewService> logger)
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

    public async Task PublishVerificationReviewTaskAsync(
        Guid taskId,
        int expertId,
        string verificationCode,
        string fileUrl,
        string fileName,
        string contentType,
        string fileType,
        string? description)
    {
        var request = new UploadFileReviewTaskDto
        {
            TaskId = taskId,
            ReviewKind = "verification",
            ExpertId = expertId,
            EntityCode = verificationCode,
            FileUrl = fileUrl,
            FileName = fileName,
            ContentType = contentType,
            FileType = fileType,
            Description = description
        };

        await PublishAsync(request);
    }

    public async Task PublishMaterialReviewTaskAsync(
        Guid taskId,
        int expertId,
        string materialCode,
        string fileUrl,
        string fileName,
        string contentType,
        string fileType,
        string? title,
        string? description,
        string? previewUrl,
        string? subjectCode,
        string? gradeCode)
    {
        var request = new UploadFileReviewTaskDto
        {
            TaskId = taskId,
            ReviewKind = "material",
            ExpertId = expertId,
            EntityCode = materialCode,
            FileUrl = fileUrl,
            FileName = fileName,
            ContentType = contentType,
            FileType = fileType,
            Title = title,
            Description = description,
            PreviewUrl = previewUrl,
            SubjectCode = subjectCode,
            GradeCode = gradeCode
        };

        await PublishAsync(request);
    }

    private async Task PublishAsync(UploadFileReviewTaskDto request)
    {
        await _semaphore.WaitAsync();
        try
        {
            await EnsureConnectedAsync();

            var bodyJson = JsonSerializer.Serialize(request, CamelCaseJsonOptions);
            if (string.IsNullOrWhiteSpace(bodyJson))
                throw new InvalidOperationException($"Nội dung review file gửi RabbitMQ đang rỗng. TaskId={request.TaskId}, EntityCode={request.EntityCode}");

            var body = Encoding.UTF8.GetBytes(bodyJson);
            if (body.Length <= 0)
                throw new InvalidOperationException($"Nội dung review file gửi RabbitMQ có độ dài bằng 0. TaskId={request.TaskId}, EntityCode={request.EntityCode}");

            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                Persistent = true
            };

            await _channel!.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: ReviewRequestQueue,
                mandatory: false,
                basicProperties: properties,
                body: body);

            _logger.LogInformation(
                "Published file review task {TaskId} for expert {ExpertId}, kind {ReviewKind}, entity {EntityCode}",
                request.TaskId,
                request.ExpertId,
                request.ReviewKind,
                request.EntityCode);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task EnsureConnectedAsync()
    {
        if (_connection is { IsOpen: true } && _channel is { IsOpen: true })
            return;

        _connection = await _connectionFactory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        await _channel.QueueDeclareAsync(
            queue: ReviewRequestQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _logger.LogInformation("Upload file review publisher connected and queue declared: '{Queue}'", ReviewRequestQueue);
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
    }
}
