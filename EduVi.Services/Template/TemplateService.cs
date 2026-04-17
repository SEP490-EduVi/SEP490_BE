using EduVi.Contracts.DTOs.Template;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using System.Text.Json;

namespace EduVi.Services.Template;

public class TemplateService : ITemplateService
{
    private const string LayoutCategory = "layout";
    private const string FreeformCategory = "freeform";

    private readonly IUnitOfWork _unitOfWork;

    public TemplateService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<TemplateResponseDto>> GetAllTemplatesAsync()
    {
        var templates = await _unitOfWork.TemplateRepository.GetAllTemplatesAsync();
        return templates.Select(MapToTemplateResponse).ToList();
    }

    public async Task<TemplateResponseDto> GetTemplateByCodeAsync(string templateCode)
    {
        var normalizedTemplateCode = NormalizeTemplateCode(templateCode);

        var template = await _unitOfWork.TemplateRepository.GetTemplateByCodeAsync(normalizedTemplateCode)
            ?? throw new KeyNotFoundException("Template not found");

        return MapToTemplateResponse(template);
    }

    public async Task<TemplateResponseDto> CreateTemplateAsync(CreateTemplateRequestDto request)
    {
        var normalizedName = NormalizeName(request.Name);
        var normalizedDescription = NormalizeDescription(request.Description);
        var normalizedCategory = NormalizeCategory(request.Category);
        ValidateSkeleton(request.Skeleton);

        var templateCode = await GenerateUniqueTemplateCodeAsync();
        var currentDateTime = DateTime.UtcNow;

        var template = new CardTemplates
        {
            TemplateCode = templateCode,
            Name = normalizedName,
            Description = normalizedDescription,
            Category = normalizedCategory,
            Skeleton = request.Skeleton.GetRawText(),
            CreatedAt = currentDateTime,
            UpdatedAt = currentDateTime
        };

        await _unitOfWork.TemplateRepository.CreateTemplateAsync(template);
        await _unitOfWork.SaveChangesAsync();

        return MapToTemplateResponse(template);
    }

    public async Task<TemplateResponseDto> UpdateTemplateAsync(string templateCode, UpdateTemplateRequestDto request)
    {
        var normalizedTemplateCode = NormalizeTemplateCode(templateCode);

        var template = await _unitOfWork.TemplateRepository.GetTemplateByCodeAsync(normalizedTemplateCode)
            ?? throw new KeyNotFoundException("Template not found");

        var normalizedName = NormalizeName(request.Name);
        var normalizedDescription = NormalizeDescription(request.Description);
        var normalizedCategory = NormalizeCategory(request.Category);
        ValidateSkeleton(request.Skeleton);

        template.Name = normalizedName;
        template.Description = normalizedDescription;
        template.Category = normalizedCategory;
        template.Skeleton = request.Skeleton.GetRawText();
        template.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.TemplateRepository.UpdateTemplate(template);
        await _unitOfWork.SaveChangesAsync();

        return MapToTemplateResponse(template);
    }

    public async Task DeleteTemplateAsync(string templateCode)
    {
        var normalizedTemplateCode = NormalizeTemplateCode(templateCode);

        var template = await _unitOfWork.TemplateRepository.GetTemplateByCodeAsync(normalizedTemplateCode)
            ?? throw new KeyNotFoundException("Template not found");

        _unitOfWork.TemplateRepository.DeleteTemplate(template);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<string> GenerateUniqueTemplateCodeAsync()
    {
        const int maximumAttempts = 10;

        for (var attemptNumber = 1; attemptNumber <= maximumAttempts; attemptNumber++)
        {
            var generatedTemplateCode = $"tpl_{Guid.NewGuid():N}"[..16];
            var existingTemplate = await _unitOfWork.TemplateRepository.GetTemplateByCodeAsync(generatedTemplateCode);
            if (existingTemplate is null)
                return generatedTemplateCode;
        }

        throw new InvalidOperationException("Không thể sinh mã template duy nhất");
    }

    private static string NormalizeTemplateCode(string templateCode)
    {
        var normalizedTemplateCode = (templateCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedTemplateCode))
            throw new InvalidOperationException("Template code không hợp lệ");
        return normalizedTemplateCode;
    }

    private static string NormalizeName(string name)
    {
        var normalizedName = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
            throw new InvalidOperationException("Tên template không được để trống");

        if (normalizedName.Length > 200)
            throw new InvalidOperationException("Tên template không được vượt quá 200 ký tự");

        return normalizedName;
    }

    private static string? NormalizeDescription(string? description)
    {
        var normalizedDescription = description?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedDescription))
            return null;

        if (normalizedDescription.Length > 500)
            throw new InvalidOperationException("Mô tả không được vượt quá 500 ký tự");

        return normalizedDescription;
    }

    private static string NormalizeCategory(string category)
    {
        var normalizedCategory = (category ?? string.Empty).Trim().ToLowerInvariant();

        if (normalizedCategory != LayoutCategory && normalizedCategory != FreeformCategory)
            throw new InvalidOperationException("Category chỉ nhận 'layout' hoặc 'freeform'");

        return normalizedCategory;
    }

    private static void ValidateSkeleton(JsonElement skeleton)
    {
        if (skeleton.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Skeleton phải là object JSON");

        if (!skeleton.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Skeleton phải chứa trường children dạng mảng");
    }

    private static TemplateResponseDto MapToTemplateResponse(CardTemplates template)
    {
        return new TemplateResponseDto
        {
            TemplateCode = template.TemplateCode,
            Name = template.Name,
            Description = template.Description,
            Category = template.Category,
            Skeleton = ParseSkeleton(template.Skeleton),
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
    }

    private static JsonElement ParseSkeleton(string skeletonJson)
    {
        if (string.IsNullOrWhiteSpace(skeletonJson))
            return JsonSerializer.Deserialize<JsonElement>("{}");

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(skeletonJson);
        }
        catch (JsonException)
        {
            return JsonSerializer.Deserialize<JsonElement>("{}");
        }
    }
}
