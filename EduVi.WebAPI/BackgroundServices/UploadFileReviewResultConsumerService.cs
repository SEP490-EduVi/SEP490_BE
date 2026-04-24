using EduVi.Contracts.DTOs.AIReview;
using EduVi.Repositories.Interfaces;
using EduVi.WebAPI.Hubs;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace EduVi.WebAPI.BackgroundServices;

/// <summary>
/// BackgroundService consume kết quả AI review file upload từ RabbitMQ.
/// Kết quả hợp lệ sẽ giữ trạng thái pending để Staff duyệt.
/// Kết quả không hợp lệ sẽ auto reject và báo cho Expert.
/// </summary>
public class UploadFileReviewResultConsumerService : BackgroundService
{
    private readonly ILogger<UploadFileReviewResultConsumerService> _logger;
    private readonly IHubContext<PipelineHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConnectionFactory _connectionFactory;

    private IConnection? _connection;
    private IChannel? _channel;

    private const string QueueName = "file.review.results";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public UploadFileReviewResultConsumerService(
        ILogger<UploadFileReviewResultConsumerService> logger,
        IHubContext<PipelineHub> hubContext,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _hubContext = hubContext;
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
        _logger.LogInformation("UploadFileReviewResultConsumer starting...");

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

                        if (!TryDeserializeReviewProgress(json, out var progress, out var parseError))
                        {
                            _logger.LogWarning("Dropping invalid file review result message. Reason: {Reason}. Payload: {Payload}", parseError, json);
                            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
                            return;
                        }

                        if (progress is null)
                        {
                            _logger.LogWarning("Received empty file review progress payload, discarding.");
                            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
                            return;
                        }

                        await HandleReviewProgressAsync(progress, stoppingToken);

                        await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing file review result message");
                        await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: CancellationToken.None);
                    }
                };

                await _channel.BasicConsumeAsync(queue: QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

                _logger.LogInformation("UploadFileReviewResultConsumer started, listening on queue '{Queue}'", QueueName);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("UploadFileReviewResultConsumer stopping...");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "UploadFileReviewResultConsumer connection failed. Retrying in 10 seconds...");

                if (_channel is not null) { try { await _channel.DisposeAsync(); } catch { } _channel = null; }
                if (_connection is not null) { try { await _connection.DisposeAsync(); } catch { } _connection = null; }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
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

        _logger.LogInformation("UploadFileReviewResultConsumer stopped");
        await base.StopAsync(cancellationToken);
    }

    private async Task HandleReviewProgressAsync(UploadFileReviewProgressDto progress, CancellationToken cancellationToken)
    {
        if (progress.Status is not "completed")
        {
            _logger.LogWarning(
                "File review task {TaskId} ended with unsupported status '{Status}'. No database change applied.",
                progress.TaskId,
                progress.Status);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        if (progress.ReviewKind.Equals("verification", StringComparison.OrdinalIgnoreCase))
        {
            var verification = await unitOfWork.StaffRepository.GetVerificationByCodeAsync(progress.EntityCode);
            if (verification is null)
            {
                _logger.LogWarning("Verification {VerificationCode} not found for task {TaskId}", progress.EntityCode, progress.TaskId);
                return;
            }

            var expertName = verification.Expert?.Expert?.FullName ?? "Không xác định";

            if (progress.Result is null || !progress.Result.IsValid)
            {
                await RejectVerificationAsync(unitOfWork, verification, progress);
                await NotifyExpertAsync(progress, expertName, false, cancellationToken);
                return;
            }

            await NotifyStaffAsync(progress, expertName, "Hồ sơ xác thực đã qua kiểm tra AI và đang chờ Staff duyệt.", true, cancellationToken);
            await NotifyExpertAsync(progress, expertName, true, cancellationToken);
            return;
        }

        if (progress.ReviewKind.Equals("material", StringComparison.OrdinalIgnoreCase))
        {
            var material = await unitOfWork.StaffRepository.GetMaterialByCodeWithDetailsAsync(progress.EntityCode);
            if (material is null)
            {
                _logger.LogWarning("Material {MaterialCode} not found for task {TaskId}", progress.EntityCode, progress.TaskId);
                return;
            }

            var expertName = material.Expert?.Expert?.FullName ?? "Không xác định";

            if (progress.Result is null || !progress.Result.IsValid)
            {
                await RejectMaterialAsync(unitOfWork, material, progress);
                await NotifyExpertAsync(progress, expertName, false, cancellationToken);
                return;
            }

            await NotifyStaffAsync(progress, expertName, "Học liệu đã qua kiểm tra AI và đang chờ Staff duyệt.", true, cancellationToken);
            await NotifyExpertAsync(progress, expertName, true, cancellationToken);
            return;
        }

        _logger.LogWarning("Unknown file review kind '{ReviewKind}' for task {TaskId}", progress.ReviewKind, progress.TaskId);
    }

    private async Task RejectVerificationAsync(
        IUnitOfWork unitOfWork,
        EduVi.Repositories.Models.ExpertVerifications verification,
        UploadFileReviewProgressDto progress)
    {
        var rejectionReason = progress.Result?.RejectionReason
            ?? progress.Result?.Summary
            ?? progress.Detail
            ?? progress.Error
            ?? "Tệp tải lên không hợp lệ";

        verification.Status = 2;
        verification.RejectionReason = rejectionReason;
        verification.ReviewedAt = DateTime.UtcNow;
        verification.ReviewedByStaffId = null;

        unitOfWork.StaffRepository.UpdateVerification(verification);

        if (!await unitOfWork.StaffRepository.HasOtherApprovedVerificationAsync(verification.ExpertId, verification.VerificationCode))
        {
            // verification đã được Include Expert ở repository, ưu tiên dùng instance đang được tracking
            // để tránh attach thêm entity cùng key trong cùng DbContext.
            if (verification.Expert is not null)
            {
                verification.Expert.IsVerified = false;
            }
            else
            {
                var expert = await unitOfWork.StaffRepository.GetExpertByIdAsync(verification.ExpertId);
                if (expert is not null)
                {
                    expert.IsVerified = false;
                }
            }
        }

        await unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Auto rejected verification {VerificationCode} for expert {ExpertId}. Reason: {Reason}",
            verification.VerificationCode,
            verification.ExpertId,
            rejectionReason);
    }

    private async Task RejectMaterialAsync(
        IUnitOfWork unitOfWork,
        EduVi.Repositories.Models.Materials material,
        UploadFileReviewProgressDto progress)
    {
        var rejectionReason = progress.Result?.RejectionReason
            ?? progress.Result?.Summary
            ?? progress.Detail
            ?? progress.Error
            ?? "Tệp tải lên không hợp lệ";

        material.ApprovalStatus = 2;
        material.RejectionReason = rejectionReason;
        material.ApproverId = null;

        unitOfWork.StaffRepository.UpdateMaterial(material);
        await unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Auto rejected material {MaterialCode} for expert {ExpertId}. Reason: {Reason}",
            material.MaterialCode,
            material.ExpertId,
            rejectionReason);
    }

    private async Task NotifyExpertAsync(
        UploadFileReviewProgressDto progress,
        string expertName,
        bool isValid,
        CancellationToken cancellationToken)
    {
        var notification = new UploadFileReviewNotificationDto
        {
            TaskId = progress.TaskId,
            ExpertId = progress.ExpertId,
            ExpertName = expertName,
            ReviewKind = progress.ReviewKind,
            EntityCode = progress.EntityCode,
            Status = isValid ? "pending_staff_review" : "auto_rejected",
            IsValid = isValid,
            RejectionReason = isValid ? null : (progress.Result?.RejectionReason ?? progress.Result?.Summary ?? progress.Detail ?? progress.Error),
            Message = isValid
                ? "File đã qua kiểm tra tự động và đang chờ Staff duyệt."
                : "File đã bị từ chối tự động. Vui lòng kiểm tra lại và tải lên file hợp lệ hơn.",
            OccurredAt = DateTime.UtcNow
        };

        await _hubContext.Clients.Group($"user_{progress.ExpertId}")
            .SendAsync("FileReviewNotification", notification, cancellationToken);
    }

    private async Task NotifyStaffAsync(
        UploadFileReviewProgressDto progress,
        string expertName,
        string message,
        bool isValid,
        CancellationToken cancellationToken)
    {
        var notification = new UploadFileReviewNotificationDto
        {
            TaskId = progress.TaskId,
            ExpertId = progress.ExpertId,
            ExpertName = expertName,
            ReviewKind = progress.ReviewKind,
            EntityCode = progress.EntityCode,
            Status = isValid ? "pending_staff_review" : "auto_rejected",
            IsValid = isValid,
            RejectionReason = isValid ? null : (progress.Result?.RejectionReason ?? progress.Result?.Summary ?? progress.Detail ?? progress.Error),
            Message = message,
            OccurredAt = DateTime.UtcNow
        };

        await _hubContext.Clients.Group("staff")
            .SendAsync("FileReviewNotification", notification, cancellationToken);
    }

    private static bool TryDeserializeReviewProgress(string json, out UploadFileReviewProgressDto? progress, out string? error)
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

            if (!TryGetInt(root, "expertId", out var expertId) && !TryGetInt(root, "expert_id", out expertId))
            {
                error = "expertId is missing or is not a valid integer";
                return false;
            }

            progress = new UploadFileReviewProgressDto
            {
                TaskId = taskId,
                ExpertId = expertId,
                ReviewKind = GetString(root, "reviewKind") ?? GetString(root, "review_kind") ?? string.Empty,
                EntityCode = GetString(root, "entityCode") ?? GetString(root, "entity_code") ?? string.Empty,
                Status = GetString(root, "status") ?? string.Empty,
                Progress = TryGetInt(root, "progress", out var progressValue) ? progressValue : 0,
                Detail = GetString(root, "detail"),
                Result = CloneResult(root, "result"),
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

    private static UploadFileReviewDecisionDto? CloneResult(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var resultElement))
            return null;

        if (resultElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        return JsonSerializer.Deserialize<UploadFileReviewDecisionDto>(resultElement.GetRawText(), JsonOptions);
    }

    private static bool TryGetGuid(JsonElement root, string propertyName, out Guid value)
    {
        value = Guid.Empty;
        if (!root.TryGetProperty(propertyName, out var element))
            return false;

        if (element.ValueKind != JsonValueKind.String)
            return false;

        return Guid.TryParse(element.GetString(), out value);
    }

    private static bool TryGetInt(JsonElement root, string propertyName, out int value)
    {
        value = 0;
        if (!root.TryGetProperty(propertyName, out var element))
            return false;

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value))
            return true;

        if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out value))
            return true;

        return false;
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
            return null;

        return element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
    }
}
