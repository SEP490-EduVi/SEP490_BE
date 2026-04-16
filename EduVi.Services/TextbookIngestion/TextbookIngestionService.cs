using EduVi.Contracts.DTOs.TextbookIngestion;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using EduVi.Services.Pipeline;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace EduVi.Services.TextbookIngestion;

public class TextbookIngestionService : ITextbookIngestionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly IRabbitMqPublisherService _publisher;
    private readonly ILogger<TextbookIngestionService> _logger;

    public TextbookIngestionService(
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        IRabbitMqPublisherService publisher,
        ILogger<TextbookIngestionService> logger)
    {
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<TextbookDocumentResponseDto> UploadTextbookDocumentAsync(int adminUserId, UploadTextbookDocumentRequestDto request)
    {
        var fileExtension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (fileExtension is not ".pdf")
            throw new InvalidOperationException("Chỉ chấp nhận tệp .pdf");

        const long maxFileSizeBytes = 100L * 1024 * 1024;
        if (request.File.Length > maxFileSizeBytes)
            throw new InvalidOperationException("Kích thước tệp không được vượt quá 100 MB");

        var completedIngestionExists = await _unitOfWork.TextbookDocumentRepository
            .ExistsCompletedAsync(request.SubjectCode, request.GradeCode);
        if (completedIngestionExists)
            throw new InvalidOperationException(
                $"Đã tồn tại một bản ghi nhập dữ liệu thành công cho {request.SubjectCode} - lớp {request.GradeCode}. Vui lòng xóa dữ liệu Neo4j trước khi tải lên lại.");

        var bucketName = _configuration["GCS:BucketName"]
            ?? throw new InvalidOperationException("Chưa cấu hình tên bucket GCS");
        var textbooksFolder = _configuration["GCS:Folders:Textbooks"] ?? "textbooks";
        var storageClient = await StorageClient.CreateAsync();

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var objectName = $"{textbooksFolder}/{request.SubjectCode}_{request.GradeCode}_{timestamp}{fileExtension}";

        using var stream = request.File.OpenReadStream();
        var uploadStart = Stopwatch.GetTimestamp();
        await storageClient.UploadObjectAsync(bucketName, objectName, request.File.ContentType, stream);
        var uploadElapsed = Stopwatch.GetElapsedTime(uploadStart);

        var gcsPath = $"gs://{bucketName}/{objectName}";
        _logger.LogInformation("GCS textbook upload completed in {ElapsedMs}ms: {GcsPath}", uploadElapsed.TotalMilliseconds, gcsPath);

        var documentCode = $"TB-{request.SubjectCode.ToUpperInvariant()}-{request.GradeCode}-{timestamp}";
        var document = new TextbookDocuments
        {
            DocumentCode = documentCode,
            SubjectCode = request.SubjectCode,
            GradeCode = request.GradeCode,
            PublishYear = request.PublishYear,
            Publisher = request.Publisher,
            OriginalFileName = request.File.FileName,
            FileUrl = gcsPath,
            Status = TextbookDocumentStatusConstants.Pending,
            Note = request.Note,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = adminUserId
        };
        await _unitOfWork.TextbookDocumentRepository.CreateAsync(document);
        await _unitOfWork.SaveChangesAsync();

        var taskId = Guid.NewGuid();
        var publishStart = Stopwatch.GetTimestamp();
        await _publisher.PublishTextbookIngestionTaskAsync(
            taskId,
            document.TextbookDocumentId,
            gcsPath,
            request.SubjectCode,
            request.GradeCode);
        var publishElapsed = Stopwatch.GetElapsedTime(publishStart);
        _logger.LogInformation(
            "RabbitMQ textbook ingestion task published in {ElapsedMs}ms for document {DocumentCode}, taskId {TaskId}",
            publishElapsed.TotalMilliseconds, documentCode, taskId);

        var response = MapToResponseDto(document);
        return response;
    }

    public async Task<List<TextbookDocumentResponseDto>> GetAllTextbookDocumentsAsync()
    {
        var documents = await _unitOfWork.TextbookDocumentRepository.GetAllAsync();
        return documents.Select(MapToResponseDto).ToList();
    }

    public async Task<TextbookDocumentResponseDto> GetTextbookDocumentByCodeAsync(string documentCode)
    {
        var document = await _unitOfWork.TextbookDocumentRepository.GetByDocumentCodeAsync(documentCode)
            ?? throw new KeyNotFoundException($"Tài liệu sách giáo khoa '{documentCode}' không tồn tại");

        return MapToResponseDto(document);
    }

    public async Task DeleteTextbookNeo4jAsync(string documentCode)
    {
        var document = await _unitOfWork.TextbookDocumentRepository.GetByDocumentCodeAsync(documentCode)
            ?? throw new KeyNotFoundException($"Tài liệu sách giáo khoa '{documentCode}' không tồn tại");

        document.Status = TextbookDocumentStatusConstants.Deleting;
        _unitOfWork.TextbookDocumentRepository.Update(document);
        await _unitOfWork.SaveChangesAsync();

        var taskId = Guid.NewGuid();
        var publishStart = Stopwatch.GetTimestamp();
        await _publisher.PublishTextbookDeletionTaskAsync(
            taskId,
            document.TextbookDocumentId,
            document.SubjectCode,
            document.GradeCode);
        var publishElapsed = Stopwatch.GetElapsedTime(publishStart);
        _logger.LogInformation(
            "RabbitMQ textbook deletion task published in {ElapsedMs}ms for document {DocumentCode}, taskId {TaskId}",
            publishElapsed.TotalMilliseconds, documentCode, taskId);
    }

    private static TextbookDocumentResponseDto MapToResponseDto(TextbookDocuments document)
    {
        return new TextbookDocumentResponseDto
        {
            DocumentCode = document.DocumentCode,
            SubjectCode = document.SubjectCode,
            GradeCode = document.GradeCode,
            PublishYear = document.PublishYear,
            Publisher = document.Publisher,
            OriginalFileName = document.OriginalFileName,
            FileUrl = document.FileUrl,
            Status = document.Status,
            StatusName = TextbookDocumentStatusConstants.GetStatusName(document.Status),
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
