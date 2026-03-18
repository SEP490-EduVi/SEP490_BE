using EduVi.Contracts.DTOs.CurriculumIngestion;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using EduVi.Services.Pipeline;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace EduVi.Services.CurriculumIngestion;

public class CurriculumIngestionService : ICurriculumIngestionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly IRabbitMqPublisherService _publisher;
    private readonly ILogger<CurriculumIngestionService> _logger;

    public CurriculumIngestionService(
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        IRabbitMqPublisherService publisher,
        ILogger<CurriculumIngestionService> logger)
    {
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<CurriculumDocumentResponseDto> UploadCurriculumDocumentAsync(int adminUserId, UploadCurriculumDocumentRequestDto request)
    {
        // File type validation: .docx only
        var fileExtension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (fileExtension is not ".docx")
            throw new InvalidOperationException("Chỉ chấp nhận file .docx");

        // File size validation: 50 MB
        const long maxFileSizeBytes = 50L * 1024 * 1024;
        if (request.File.Length > maxFileSizeBytes)
            throw new InvalidOperationException("Kích thước file không được vượt quá 50 MB");

        var completedIngestionExists = await _unitOfWork.CurriculumDocumentRepository
            .ExistsCompletedAsync(request.SubjectCode, request.EducationLevel, request.CurriculumYear);

        var bucketName = _configuration["GCS:BucketName"]
            ?? throw new InvalidOperationException("GCS BucketName not configured");
        var curriculaFolder = _configuration["GCS:Folders:Curricula"] ?? "curricula";
        var storageClient = await StorageClient.CreateAsync();

        // Upload to GCS
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var objectName = $"{curriculaFolder}/{request.SubjectCode}_{request.CurriculumYear}_{timestamp}{fileExtension}";

        using var stream = request.File.OpenReadStream();
        var uploadStart = Stopwatch.GetTimestamp();
        await storageClient.UploadObjectAsync(
            bucketName,
            objectName,
            request.File.ContentType,
            stream);
        var uploadElapsed = Stopwatch.GetElapsedTime(uploadStart);

        var gcsPath = $"gs://{bucketName}/{objectName}";
        _logger.LogInformation("GCS curriculum upload completed in {ElapsedMs}ms: {GcsPath}", uploadElapsed.TotalMilliseconds, gcsPath);

        // Create DB record (always new — re-uploads create separate records)
        var documentCode = $"CT-{request.SubjectCode.ToUpperInvariant()}-{request.CurriculumYear}-{timestamp}";
        var document = new CurriculumDocuments
        {
            DocumentCode = documentCode,
            SubjectCode = request.SubjectCode,
            EducationLevel = request.EducationLevel,
            CurriculumYear = request.CurriculumYear,
            OriginalFileName = request.File.FileName,
            FileUrl = gcsPath,
            Status = CurriculumDocumentStatusConstants.Pending,
            Note = request.Note,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = adminUserId
        };
        await _unitOfWork.CurriculumDocumentRepository.CreateAsync(document);
        await _unitOfWork.SaveChangesAsync();

        // Publish to RabbitMQ
        var taskId = Guid.NewGuid();
        var publishStart = Stopwatch.GetTimestamp();
        await _publisher.PublishCurriculumIngestionTaskAsync(
            taskId,
            document.CurriculumDocumentId,
            gcsPath,
            request.SubjectCode,
            request.EducationLevel,
            request.CurriculumYear);
        var publishElapsed = Stopwatch.GetElapsedTime(publishStart);
        _logger.LogInformation("RabbitMQ curriculum ingestion task published in {ElapsedMs}ms for document {DocumentCode}, taskId {TaskId}",
            publishElapsed.TotalMilliseconds, documentCode, taskId);

        var response = MapToResponseDto(document);
        if (completedIngestionExists)
            response.Warning = $"Đã tồn tại một bản ghi ingestion thành công cho {request.SubjectCode} - {request.EducationLevel} - {request.CurriculumYear}. Tác vụ mới vẫn sẽ được xử lý.";

        return response;
    }

    public async Task<List<CurriculumDocumentResponseDto>> GetAllCurriculumDocumentsAsync()
    {
        var documents = await _unitOfWork.CurriculumDocumentRepository.GetAllAsync();
        return documents.Select(MapToResponseDto).ToList();
    }

    public async Task<CurriculumDocumentResponseDto> GetCurriculumDocumentByCodeAsync(string documentCode)
    {
        var document = await _unitOfWork.CurriculumDocumentRepository.GetByDocumentCodeAsync(documentCode)
            ?? throw new KeyNotFoundException($"Curriculum document '{documentCode}' không tồn tại");

        return MapToResponseDto(document);
    }

    private static CurriculumDocumentResponseDto MapToResponseDto(CurriculumDocuments document)
    {
        return new CurriculumDocumentResponseDto
        {
            DocumentCode = document.DocumentCode,
            SubjectCode = document.SubjectCode,
            EducationLevel = document.EducationLevel,
            CurriculumYear = document.CurriculumYear,
            OriginalFileName = document.OriginalFileName,
            FileUrl = document.FileUrl,
            Status = document.Status,
            StatusName = CurriculumDocumentStatusConstants.GetStatusName(document.Status),
            Note = document.Note,
            Stats = ParseJson(document.Stats),
            ErrorMessage = document.ErrorMessage,
            CreatedAt = document.CreatedAt
        };
    }

    private static JsonElement? ParseJson(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return JsonSerializer.Deserialize<JsonElement>(value);
    }
}
