using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Pipeline;
using EduVi.Repositories.Interfaces;
using EduVi.Services.Pipeline;
using EduVi.WebAPI.Hubs;
using Microsoft.AspNetCore.SignalR;
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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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

                        var progress = JsonSerializer.Deserialize<PipelineProgressDto>(json, JsonOptions);

                        if (progress is not null)
                        {
                            var redisDb = _redis.GetDatabase();
                            RedisValue existingMeta = RedisValue.Null;

                            if (progress.TaskId != Guid.Empty)
                            {
                                existingMeta = await redisDb.StringGetAsync($"pipeline:status:{progress.TaskId}");
                            }

                            // Resolve productId from Redis if worker didn't include it
                            if (progress.ProductId.GetValueOrDefault() == 0 && progress.TaskId != Guid.Empty)
                            {
                                if (!existingMeta.IsNullOrEmpty)
                                {
                                    var originalTask = JsonSerializer.Deserialize<PipelineProgressDto>(existingMeta!, JsonOptions);
                                    if (originalTask?.ProductId is > 0)
                                        progress.ProductId = originalTask.ProductId;
                                }
                            }

                            // Push to user's SignalR group (strip heavy storage-only fields from lesson analysis result)
                            await _hubContext.Clients
                                .Group($"user_{progress.UserId}")
                                .SendAsync("PipelineProgress", BuildSignalRPayload(progress), stoppingToken);

                            // Persist to DB when completed or failed
                            if (progress.Status is "completed" or "failed" && progress.ProductId.GetValueOrDefault() > 0)
                            {
                                await UpdateProductInDatabaseAsync(progress);
                            }

                            // Update status in Redis after DB persistence.
                            // This keeps original task metadata (e.g. productVideoCode/requestId)
                            // available during DB update correlation.
                            await redisDb.StringSetAsync(
                                $"pipeline:status:{progress.TaskId}",
                                BuildRedisProgressPayload(progress, existingMeta.IsNullOrEmpty ? null : (string)existingMeta!),
                                TimeSpan.FromHours(1));

                            _logger.LogInformation(
                                "Pushed progress for task {TaskId} (Product {ProductId}) to user {UserId}: {Status} ({Progress}%)",
                                progress.TaskId, progress.ProductId, progress.UserId, progress.Status, progress.Progress);
                        }
                        else
                        {
                            _logger.LogWarning("Received malformed or undeserializable pipeline result message, discarding.");
                        }

                        // CancellationToken.None: ack/nack must always complete even during shutdown.
                        // Passing stoppingToken here would throw OperationCanceledException before the
                        // ack is sent, leaving the message unacknowledged and causing infinite redelivery.
                        await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing pipeline result message");
                        await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: CancellationToken.None);
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

        _logger.LogInformation("PipelineResultConsumer stopped");
        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Cập nhật Product status và lưu evaluation result vào DB
    /// Dùng IServiceScopeFactory vì BackgroundService là singleton, DbContext là scoped
    /// </summary>
    private async Task UpdateProductInDatabaseAsync(PipelineProgressDto progress)
    {
        var productId = progress.ProductId.GetValueOrDefault();
        if (productId <= 0)
        {
            _logger.LogWarning("ProductId missing for task {TaskId}", progress.TaskId);
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var product = await unitOfWork.PipelineRepository
                .GetProductByIdAsync(productId);

            if (product is null)
            {
                _logger.LogWarning("Product {ProductId} not found for task {TaskId}", productId, progress.TaskId);
                return;
            }

            if (progress.Status == "completed")
            {
                var resultJson = progress.Result is not null
                    ? JsonSerializer.Serialize(progress.Result)
                    : null;

                // Determine which pipeline step completed based on the step field
                if (progress.Step == "video_completed" || IsVideoGenerationResult(progress.Result))
                {
                    // Video generation completed
                    await UpsertProductVideoCompletedAsync(unitOfWork, progress, product.ProductId);

                    _logger.LogInformation("Product {ProductId} video generated successfully", product.ProductId);
                }
                else if (progress.Step == "slides_completed")
                {
                    // Slide generation completed — store SlideDocument
                    product.Status = ProductStatusConstants.SlidesGenerated;
                    product.SlideDocument = resultJson;
                    product.SlideGeneratedAt = DateTime.UtcNow;

                    _logger.LogInformation("Product {ProductId} slides generated successfully", product.ProductId);
                }
                else
                {
                    // Lesson analysis completed — store EvaluationResult (lesson_plan_text excluded) + LessonPlanText separately
                    product.Status = ProductStatusConstants.Evaluated;
                    product.EvaluatedAt = DateTime.UtcNow;

                    if (progress.Result is JsonElement resultElement)
                    {
                        if (resultElement.TryGetProperty("lesson_plan_text", out var lessonPlanTextElement))
                            product.LessonPlanText = lessonPlanTextElement.GetString();

                        // Serialize without lesson_plan_text to avoid storing the large text blob twice
                        var strippedResult = resultElement.EnumerateObject()
                            .Where(property => property.Name != "lesson_plan_text")
                            .ToDictionary(property => property.Name, property => property.Value);
                        product.EvaluationResult = JsonSerializer.Serialize(strippedResult);
                    }
                    else if (progress.Result is not null)
                    {
                        product.EvaluationResult = JsonSerializer.Serialize(progress.Result);
                    }

                    _logger.LogInformation("Product {ProductId} marked as EVALUATED", product.ProductId);
                }
            }
            else if (progress.Status == "failed")
            {
                var isVideoGeneration = await IsVideoGenerationTaskAsync(progress.TaskId);
                // Check Redis to determine which pipeline this task belongs to
                var isSlideGeneration = await IsSlideGenerationTaskAsync(progress.TaskId);

                if (isVideoGeneration)
                {
                    await UpsertProductVideoFailedAsync(unitOfWork, progress, product.ProductId);
                    _logger.LogWarning("Product {ProductId} video generation FAILED: {Error}", product.ProductId, progress.Error);
                }
                else if (isSlideGeneration)
                {
                    product.Status = ProductStatusConstants.SlidesFailed;
                    _logger.LogWarning("Product {ProductId} slide generation FAILED: {Error}", product.ProductId, progress.Error);
                }
                else
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
            }

            unitOfWork.PipelineRepository.UpdateProduct(product);
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Product {ProductId} in database for task {TaskId}",
                productId, progress.TaskId);
        }
    }

    /// <summary>
    /// Strips lesson_plan_text and textbook_sections from lesson analysis completed results
    /// before pushing to SignalR — those fields are storage-only and too large for FE progress events.
    /// </summary>
    private static PipelineProgressDto BuildSignalRPayload(PipelineProgressDto progress)
    {
        if (progress.Status != "completed"
            || progress.Step == "slides_completed"
            || progress.Result is not JsonElement resultElement
            || resultElement.ValueKind != JsonValueKind.Object)
            return progress;

        var strippedResult = resultElement.EnumerateObject()
            .Where(p => p.Name is not "lesson_plan_text" and not "textbook_sections")
            .ToDictionary(p => p.Name, p => (object?)p.Value.Clone());

        return new PipelineProgressDto
        {
            TaskId = progress.TaskId,
            UserId = progress.UserId,
            ProductId = progress.ProductId,
            Status = progress.Status,
            Step = progress.Step,
            Progress = progress.Progress,
            Detail = progress.Detail,
            Result = strippedResult,
            Error = progress.Error
        };
    }

    /// <summary>
    /// Kiểm tra task có phải slide generation không dựa trên Redis metadata
    /// (PipelineService lưu step = "slide_generation" khi tạo task)
    /// </summary>
    private async Task<bool> IsSlideGenerationTaskAsync(Guid taskId)
    {
        try
        {
            var redisDb = _redis.GetDatabase();
            var data = await redisDb.StringGetAsync($"pipeline:status:{taskId}");
            if (data.IsNullOrEmpty)
                return false;

            using var doc = JsonDocument.Parse((string)data!);
            if (doc.RootElement.TryGetProperty("step", out var stepElement))
            {
                var step = stepElement.GetString();
                return step == "slide_generation"
                    || step == "planning"
                    || step == "generating_slides"
                    || step == "assembling"
                    || step == "slides_completed";
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> IsVideoGenerationTaskAsync(Guid taskId)
    {
        try
        {
            var redisDb = _redis.GetDatabase();
            var data = await redisDb.StringGetAsync($"pipeline:status:{taskId}");
            if (data.IsNullOrEmpty)
                return false;

            using var doc = JsonDocument.Parse((string)data!);
            if (doc.RootElement.TryGetProperty("step", out var stepElement))
            {
                var step = stepElement.GetString();
                return step == "video_generation"
                    || step == "video_processing"
                    || step == "video_completed";
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsVideoGenerationResult(object? result)
    {
        if (result is not JsonElement resultElement || resultElement.ValueKind != JsonValueKind.Object)
            return false;

        return resultElement.TryGetProperty("video_url", out _)
            || resultElement.TryGetProperty("request_id", out _)
            || resultElement.TryGetProperty("interactions", out _);
    }

    private async Task UpsertProductVideoCompletedAsync(IUnitOfWork unitOfWork, PipelineProgressDto progress, int productId)
    {
        var productVideoCode = await ResolveProductVideoCodeAsync(progress);
        var videoName = await ResolveVideoNameAsync(progress);

        if (string.IsNullOrWhiteSpace(productVideoCode) && progress.TaskId != Guid.Empty)
            productVideoCode = $"video_task_{progress.TaskId}";

        if (string.IsNullOrWhiteSpace(productVideoCode))
        {
            _logger.LogWarning(
                "Cannot correlate video result to ProductVideos row. TaskId={TaskId}, ProductId={ProductId}",
                progress.TaskId,
                productId);
            return;
        }

        var productVideo = await unitOfWork.PipelineRepository.GetProductVideoByCodeAsync(productVideoCode!);

        if (productVideo is null)
        {
            productVideo = await unitOfWork.PipelineRepository.CreateProductVideoAsync(new EduVi.Repositories.Models.ProductVideos
            {
                ProductId = productId,
                ProductVideoCode = productVideoCode,
                VideoName = videoName,
                Status = VideoStatusConstants.Completed,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (string.IsNullOrWhiteSpace(productVideo.VideoName) && !string.IsNullOrWhiteSpace(videoName))
            productVideo.VideoName = videoName;

        productVideo.Status = VideoStatusConstants.Completed;
        productVideo.UpdatedAt = DateTime.UtcNow;
        productVideo.CompletedAt = DateTime.UtcNow;

        if (progress.Result is JsonElement videoResultElement)
        {
            if (videoResultElement.TryGetProperty("video_url", out var videoUrlElement))
                productVideo.VideoUrl = videoUrlElement.GetString();

            if (videoResultElement.TryGetProperty("duration", out var durationElement) &&
                durationElement.TryGetDouble(out var videoDuration))
                productVideo.Duration = videoDuration;

            if (videoResultElement.TryGetProperty("request_id", out var requestIdElement))
                productVideo.ProductVideoCode = requestIdElement.GetString() ?? productVideo.ProductVideoCode;

            if (videoResultElement.TryGetProperty("interactions", out var interactionsElement))
                productVideo.Interactions = interactionsElement.GetRawText();
        }

        unitOfWork.PipelineRepository.UpdateProductVideo(productVideo);
    }

    private async Task UpsertProductVideoFailedAsync(IUnitOfWork unitOfWork, PipelineProgressDto progress, int productId)
    {
        var productVideoCode = await ResolveProductVideoCodeAsync(progress);
        var videoName = await ResolveVideoNameAsync(progress);

        if (string.IsNullOrWhiteSpace(productVideoCode) && progress.TaskId != Guid.Empty)
            productVideoCode = $"video_task_{progress.TaskId}";

        if (string.IsNullOrWhiteSpace(productVideoCode))
        {
            _logger.LogWarning(
                "Cannot correlate failed video result to ProductVideos row. TaskId={TaskId}, ProductId={ProductId}",
                progress.TaskId,
                productId);
            return;
        }

        var productVideo = await unitOfWork.PipelineRepository.GetProductVideoByCodeAsync(productVideoCode!);

        if (productVideo is null)
        {
            productVideo = await unitOfWork.PipelineRepository.CreateProductVideoAsync(new EduVi.Repositories.Models.ProductVideos
            {
                ProductId = productId,
                ProductVideoCode = productVideoCode,
                VideoName = videoName,
                Status = VideoStatusConstants.Failed,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (string.IsNullOrWhiteSpace(productVideo.VideoName) && !string.IsNullOrWhiteSpace(videoName))
            productVideo.VideoName = videoName;

        productVideo.Status = VideoStatusConstants.Failed;
        productVideo.ErrorMessage = progress.Error;
        productVideo.UpdatedAt = DateTime.UtcNow;
        unitOfWork.PipelineRepository.UpdateProductVideo(productVideo);
    }

    private async Task<string?> ResolveProductVideoCodeAsync(PipelineProgressDto progress)
    {
        if (progress.Result is JsonElement resultElement
            && resultElement.ValueKind == JsonValueKind.Object)
        {
            if (resultElement.TryGetProperty("request_id", out var requestIdElement))
            {
                var requestIdFromResult = requestIdElement.GetString();
                if (!string.IsNullOrWhiteSpace(requestIdFromResult))
                    return requestIdFromResult;
            }

            if (resultElement.TryGetProperty("requestId", out var requestIdCamelElement))
            {
                var requestIdFromResult = requestIdCamelElement.GetString();
                if (!string.IsNullOrWhiteSpace(requestIdFromResult))
                    return requestIdFromResult;
            }
        }

        try
        {
            var redisDb = _redis.GetDatabase();
            var data = await redisDb.StringGetAsync($"pipeline:status:{progress.TaskId}");
            if (data.IsNullOrEmpty)
                return null;

            using var doc = JsonDocument.Parse((string)data!);
            if (doc.RootElement.TryGetProperty("productVideoCode", out var productVideoCodeMetaElement))
                return productVideoCodeMetaElement.GetString();

            if (doc.RootElement.TryGetProperty("requestId", out var requestIdMetaElement))
                return requestIdMetaElement.GetString();

            if (doc.RootElement.TryGetProperty("request_id", out var requestIdSnakeMetaElement))
                return requestIdSnakeMetaElement.GetString();
        }
        catch
        {
            return null;
        }

        return null;
    }

    private async Task<string?> ResolveVideoNameAsync(PipelineProgressDto progress)
    {
        try
        {
            var redisDb = _redis.GetDatabase();
            var data = await redisDb.StringGetAsync($"pipeline:status:{progress.TaskId}");
            if (data.IsNullOrEmpty)
                return null;

            using var doc = JsonDocument.Parse((string)data!);
            if (doc.RootElement.TryGetProperty("videoName", out var videoNameElement))
            {
                var videoName = videoNameElement.GetString();
                if (!string.IsNullOrWhiteSpace(videoName))
                    return videoName;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildRedisProgressPayload(PipelineProgressDto progress, string? existingMetaJson)
    {
        string? requestId = null;
        string? productVideoCode = null;
        string? videoName = null;
        string? createdAt = null;

        if (!string.IsNullOrWhiteSpace(existingMetaJson))
        {
            try
            {
                using var existingMetaDocument = JsonDocument.Parse(existingMetaJson);
                var existingRoot = existingMetaDocument.RootElement;

                if (existingRoot.TryGetProperty("requestId", out var requestIdElement))
                    requestId = requestIdElement.GetString();

                if (existingRoot.TryGetProperty("productVideoCode", out var productVideoCodeElement))
                    productVideoCode = productVideoCodeElement.GetString();

                if (existingRoot.TryGetProperty("videoName", out var videoNameElement))
                    videoName = videoNameElement.GetString();

                if (existingRoot.TryGetProperty("createdAt", out var createdAtElement))
                    createdAt = createdAtElement.GetString();
            }
            catch
            {
                // Ignore parse errors and fall back to minimal payload.
            }
        }

        return JsonSerializer.Serialize(new
        {
            taskId = progress.TaskId,
            userId = progress.UserId,
            productId = progress.ProductId,
            status = progress.Status,
            step = progress.Step,
            progress = progress.Progress,
            detail = progress.Detail,
            result = progress.Result,
            error = progress.Error,
            requestId,
            productVideoCode,
            videoName,
            createdAt
        });
    }
}
