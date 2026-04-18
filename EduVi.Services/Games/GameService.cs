using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Games.Request;
using EduVi.Contracts.DTOs.Games.Response;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using EduVi.Services.Pipeline;
using StackExchange.Redis;
using System.Diagnostics;
using System.Linq;
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
        var productCode = NormalizeProductCode(request.ProductCode);

        var templateId = (request.TemplateId ?? string.Empty).Trim();
        if (!IsSupportedTemplate(templateId))
            throw new InvalidOperationException("Mã mẫu trò chơi không hợp lệ");

        var template = await _unitOfWork.GameTemplateRepository.GetTemplateByCodeAsync(templateId)
            ?? throw new InvalidOperationException("Mẫu trò chơi không tồn tại hoặc đã bị ẩn");

        var slideEditedDocumentUrl = (request.SlideEditedDocumentUrl ?? string.Empty).Trim();
        if (!IsSupportedGcsUrl(slideEditedDocumentUrl))
            throw new InvalidOperationException("Đường dẫn tài liệu slide đã chỉnh sửa phải là đường dẫn GCS hợp lệ (gs://... hoặc https://storage.googleapis.com/...)");

        var roundCount = request.RoundCount ?? 1;
        if (roundCount <= 0)
            throw new InvalidOperationException("Số vòng chơi phải lớn hơn 0");

        var taskId = Guid.NewGuid();

        var teacherId = await GetTeacherEntityIdAsync(userId);
        var product = await _unitOfWork.PipelineRepository.GetProductByCodeAndTeacherAsync(productCode, teacherId)
            ?? throw new KeyNotFoundException($"Không tìm thấy product với mã {productCode}");

        if (string.IsNullOrWhiteSpace(product.SlideEditedDocument))
            throw new InvalidOperationException("Product chưa có slide đã chỉnh sửa để tạo game");

        if (!string.Equals(product.SlideEditedDocument?.Trim(), slideEditedDocumentUrl, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("slideEditedDocumentUrl không khớp với slide của productCode đã chọn");

        var productGameCode = BuildProductGameCode(productCode, taskId);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var consumed = await _unitOfWork.PaymentRepository.ConsumeGameQuotaAsync(teacherId, amount: 1);
            if (!consumed)
                throw new InvalidOperationException("Bạn không còn đủ lượt tạo game. Vui lòng mua thêm gói để tiếp tục.");

            await _unitOfWork.GameRepository.CreateProductGameAsync(new ProductGames
            {
                ProductId = product.ProductId,
                ProductGameCode = productGameCode,
                TaskId = taskId,
                ProductName = product.ProductName,
                TemplateCode = template.TemplateCode ?? templateId,
                RoundCount = roundCount,
                Status = GameStatusConstants.Queued,
                CreatedAt = DateTime.UtcNow
            });

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }

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
        var taskMeta = JsonSerializer.Serialize(new
        {
            taskId,
            userId = userId.ToString(),
            templateId = template.TemplateCode ?? templateId,
            status = "queued",
            step = "game_generation",
            progress = 0,
            detail = (string?)null,
            result = (object?)null,
            error = (string?)null,
            productCode,
            productGameCode,
            productName = product.ProductName,
            roundCount
        });
        var redisStart = Stopwatch.GetTimestamp();
        await db.StringSetAsync($"game:status:{taskId}", taskMeta, TimeSpan.FromHours(1));
        _ = Stopwatch.GetElapsedTime(redisStart);

        await _publisher.PublishGameGenerationTaskAsync(taskId, message);

        return new GameTaskResponseDto
        {
            TaskId = taskId,
            GameCode = productGameCode,
            ProductGameCode = productGameCode,
            ProductCode = productCode,
            ProductName = product.ProductName,
            TemplateId = template.TemplateCode ?? templateId,
            RoundCount = roundCount,
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

    public async Task<List<GameSummaryDto>> GetGamesByTeacherAsync(int userId)
    {
        var teacherId = await GetTeacherEntityIdAsync(userId);
        var productGames = await _unitOfWork.GameRepository.GetActiveProductGamesByTeacherAsync(teacherId);

        return productGames
            .Select(productGame => new GameSummaryDto
            {
                GameCode = productGame.ProductGameCode,
                ProductGameCode = productGame.ProductGameCode,
                ProductCode = productGame.Product?.ProductCode ?? string.Empty,
                ProductName = productGame.ProductName,
                TemplateCode = productGame.TemplateCode,
                RoundCount = productGame.RoundCount,
                Status = GameStatusConstants.GetStatusName(productGame.Status),
                CreatedAt = productGame.CreatedAt,
                UpdatedAt = productGame.UpdatedAt,
                CompletedAt = productGame.CompletedAt
            })
            .ToList();
    }

    public async Task<GameDetailDto> GetGameByCodeAsync(int userId, string gameCode)
    {
        var teacherId = await GetTeacherEntityIdAsync(userId);
        var productGame = await _unitOfWork.GameRepository.GetProductGameByCodeAndTeacherAsync(gameCode, teacherId)
            ?? throw new KeyNotFoundException($"Không tìm thấy game với mã {gameCode}");

        if (productGame.Status == GameStatusConstants.Deleted)
            throw new KeyNotFoundException($"Không tìm thấy game với mã {gameCode}");

        return new GameDetailDto
        {
            GameCode = productGame.ProductGameCode,
            ProductGameCode = productGame.ProductGameCode,
            ProductCode = productGame.Product?.ProductCode ?? string.Empty,
            ProductName = productGame.ProductName,
            TemplateCode = productGame.TemplateCode,
            RoundCount = productGame.RoundCount,
            Status = GameStatusConstants.GetStatusName(productGame.Status),
            TaskId = productGame.TaskId,
            Result = ParseJson(productGame.ResultJson),
            ErrorMessage = productGame.ErrorMessage,
            CreatedAt = productGame.CreatedAt,
            UpdatedAt = productGame.UpdatedAt,
            CompletedAt = productGame.CompletedAt
        };
    }

    public async Task SoftDeleteGameAsync(int userId, string gameCode)
    {
        var teacherId = await GetTeacherEntityIdAsync(userId);
        var productGame = await _unitOfWork.GameRepository.GetProductGameByCodeAndTeacherAsync(gameCode, teacherId)
            ?? throw new KeyNotFoundException($"Không tìm thấy game với mã {gameCode}");

        if (productGame.Status == GameStatusConstants.Deleted)
            throw new KeyNotFoundException($"Không tìm thấy game với mã {gameCode}");

        productGame.Status = GameStatusConstants.Deleted;
        productGame.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.GameRepository.UpdateProductGame(productGame);
        await _unitOfWork.SaveChangesAsync();
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

    private static string NormalizeProductCode(string productCode)
    {
        var normalizedProductCode = (productCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedProductCode))
            throw new InvalidOperationException("ProductCode không được để trống");
        return normalizedProductCode;
    }

    private static string BuildProductGameCode(string productCode, Guid taskId)
    {
        var cleanedProductCode = new string((productCode ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .ToArray());

        if (string.IsNullOrWhiteSpace(cleanedProductCode))
            cleanedProductCode = "product";

        if (cleanedProductCode.Length > 24)
            cleanedProductCode = cleanedProductCode[..24];

        var taskSegment = taskId.ToString("N")[..8];
        return $"pgame_{cleanedProductCode}_{taskSegment}";
    }

    private static JsonElement? ParseJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(value);
        }
        catch (JsonException)
        {
            return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(value));
        }
    }

    private async Task<int> GetTeacherEntityIdAsync(int userId)
    {
        var user = await _unitOfWork.AuthenticationRepository.GetUserByIdAsync(userId)
            ?? throw new InvalidOperationException("Không tìm thấy người dùng");

        return user.Teachers?.TeacherId
            ?? throw new InvalidOperationException("Chỉ giáo viên mới có thể tạo game");
    }
}
