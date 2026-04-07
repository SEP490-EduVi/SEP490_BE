using EduVi.Contracts.DTOs.Games.Request;
using EduVi.Contracts.DTOs.Games.Response;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using EduVi.Services.Pipeline;
using StackExchange.Redis;
using System.Diagnostics;
using System.Text.Json;

namespace EduVi.Services.Games;

public class GameService : IGameService
{
    private const string TemplateHoverSelect = "HOVER_SELECT";
    private const string TemplateDragDrop = "DRAG_DROP";
    private const string TemplateSnakeDuel = "SNAKE_DUEL";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IRabbitMqPublisherService _publisher;
    private readonly IConnectionMultiplexer _redis;

    public GameService(IUnitOfWork unitOfWork, IRabbitMqPublisherService publisher, IConnectionMultiplexer redis)
    {
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _redis = redis;
    }

    public async Task<GameTaskResponseDto> CreatePlayableGameTaskAsync(int userId, GameConfigRequest request)
    {
        var templateId = (request.TemplateId ?? string.Empty).Trim();
        if (!IsSupportedTemplate(templateId))
            throw new InvalidOperationException("TemplateId không hợp lệ");

        var template = await _unitOfWork.GameTemplateRepository.GetTemplateByCodeAsync(templateId)
            ?? throw new InvalidOperationException("Template không tồn tại hoặc đã bị ẩn");

        var slideEditedDocumentUrl = (request.SlideEditedDocumentUrl ?? string.Empty).Trim();
        if (!IsSupportedGcsUrl(slideEditedDocumentUrl))
            throw new InvalidOperationException("SlideEditedDocumentUrl phải là GCS URL hợp lệ (gs://... hoặc https://storage.googleapis.com/...)");

        var roundCount = request.RoundCount ?? 1;
        if (roundCount <= 0)
            throw new InvalidOperationException("RoundCount phải lớn hơn 0");

        var taskId = Guid.NewGuid();

        await GetTeacherEntityIdAsync(userId);

        var message = new GameGenerationRequestMessage
        {
            TaskId = taskId.ToString(),
            UserId = userId.ToString(),
            TemplateId = template.TemplateCode ?? templateId,
            TemplateVersion = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            TemplateJson = template.TemplateJson,
            SlideEditedDocumentUrl = slideEditedDocumentUrl,
            RoundCount = roundCount,
            CreatedAt = DateTime.UtcNow.ToString("o")
        };

        var db = _redis.GetDatabase();
        var taskMeta = JsonSerializer.Serialize(new GameProgressDto
        {
            TaskId = taskId,
            UserId = userId.ToString(),
            TemplateId = template.TemplateCode ?? templateId,
            Status = "queued",
            Step = "game_generation",
            Progress = 0,
            Detail = null,
            Result = null,
            Error = null
        });
        var redisStart = Stopwatch.GetTimestamp();
        await db.StringSetAsync($"game:status:{taskId}", taskMeta, TimeSpan.FromHours(1));
        _ = Stopwatch.GetElapsedTime(redisStart);

        await _publisher.PublishGameGenerationTaskAsync(taskId, message);

        return new GameTaskResponseDto
        {
            TaskId = taskId,
            TemplateId = template.TemplateCode ?? templateId,
            Status = "queued"
        };
    }

    public async Task<GameProgressDto?> GetGameStatusAsync(Guid taskId)
    {
        var db = _redis.GetDatabase();
        var data = await db.StringGetAsync($"game:status:{taskId}");

        if (data.IsNullOrEmpty)
            return null;

        return JsonSerializer.Deserialize<GameProgressDto>(data!, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private static bool IsSupportedTemplate(string templateId)
    {
        return templateId == TemplateHoverSelect
            || templateId == TemplateDragDrop
            || templateId == TemplateSnakeDuel;
    }

    private static bool IsSupportedGcsUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.StartsWith("gs://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://storage.googleapis.com/", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<int> GetTeacherEntityIdAsync(int userId)
    {
        var user = await _unitOfWork.AuthenticationRepository.GetUserByIdAsync(userId)
            ?? throw new InvalidOperationException("Không tìm thấy người dùng");

        return user.Teachers?.TeacherId
            ?? throw new InvalidOperationException("Chỉ giáo viên mới có thể tạo game");
    }
}
