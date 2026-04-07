using EduVi.Contracts.DTOs.Pipeline;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace EduVi.Services.Pipeline;

public class InputDocumentService : IInputDocumentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InputDocumentService> _logger;

    public InputDocumentService(
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<InputDocumentService> logger)
    {
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<InputDocumentResponseDto> UploadInputDocumentAsync(int teacherId, UploadInputDocumentRequestDto request)
    {
        var project = await _unitOfWork.PipelineRepository
            .GetProjectByCodeAndTeacherAsync(request.ProjectCode, teacherId)
            ?? throw new InvalidOperationException($"Project '{request.ProjectCode}' không tồn tại hoặc không thuộc về bạn");

        var subject = await _unitOfWork.CurriculumRepository.GetSubjectByCodeAsync(request.SubjectCode)
            ?? throw new InvalidOperationException($"Subject '{request.SubjectCode}' không tồn tại");
        var grade = await _unitOfWork.CurriculumRepository.GetGradeByCodeAsync(request.GradeCode)
            ?? throw new InvalidOperationException($"Grade '{request.GradeCode}' không tồn tại");

        int? lessonId = null;
        string lessonCodePart = string.Empty;
        if (request.LessonCode is not null)
        {
            var lesson = await _unitOfWork.CurriculumRepository.GetLessonByCodeAsync(request.LessonCode)
                ?? throw new InvalidOperationException($"Lesson '{request.LessonCode}' không tồn tại");
            lessonId = lesson.LessonId;
            lessonCodePart = $"_{request.LessonCode}";
        }

        var bucketName = _configuration["GCS:BucketName"]
            ?? throw new InvalidOperationException("GCS BucketName not configured");
        var storageClient = await StorageClient.CreateAsync();

        var fileExtension = Path.GetExtension(request.File.FileName);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var fileName = $"{request.SubjectCode}_{request.GradeCode}{lessonCodePart}_{timestamp}{fileExtension}";
        var objectName = $"user_inputs/{teacherId}/{request.ProjectCode}/{fileName}";

        using var stream = request.File.OpenReadStream();
        var uploadStart = Stopwatch.GetTimestamp();
        await storageClient.UploadObjectAsync(
            bucketName,
            objectName,
            request.File.ContentType,
            stream);
        var uploadElapsed = Stopwatch.GetElapsedTime(uploadStart);

        var gcsPath = $"gs://{bucketName}/{objectName}";
        _logger.LogInformation("GCS upload completed in {ElapsedMs}ms: {GcsPath}", uploadElapsed.TotalMilliseconds, gcsPath);

        var documentCode = $"doc_{request.ProjectCode}_{request.SubjectCode}_{request.GradeCode}{lessonCodePart}_{timestamp}";

        var document = new InputDocuments
        {
            TeacherId = teacherId,
            ProjectId = project.ProjectId,
            Title = request.Title,
            DocumentCode = documentCode,
            SubjectId = subject.SubjectId,
            GradeId = grade.GradeId,
            LessonId = lessonId,
            FilePath = gcsPath,
            UploadDate = DateTime.UtcNow
        };
        await _unitOfWork.InputDocumentRepository.CreateInputDocumentAsync(document);

        await _unitOfWork.SaveChangesAsync();

        var savedDocument = await _unitOfWork.InputDocumentRepository
            .GetInputDocumentByIdAsync(document.DocumentId);

        return MapToInputDocumentResponse(savedDocument!);
    }

    public async Task<List<InputDocumentResponseDto>> GetInputDocumentsByTeacherAsync(int teacherId)
    {
        var documents = await _unitOfWork.InputDocumentRepository
            .GetInputDocumentsByTeacherAsync(teacherId);

        return documents.Select(MapToInputDocumentResponse).ToList();
    }

    public async Task<List<InputDocumentResponseDto>> GetInputDocumentsByProjectCodeAsync(int teacherId, string projectCode)
    {
        var project = await _unitOfWork.PipelineRepository
            .GetProjectByCodeAndTeacherAsync(projectCode, teacherId)
            ?? throw new KeyNotFoundException($"Project '{projectCode}' không tồn tại hoặc không thuộc về bạn");

        var documents = await _unitOfWork.InputDocumentRepository
            .GetInputDocumentsByTeacherAndProjectAsync(teacherId, project.ProjectId);

        return documents.Select(MapToInputDocumentResponse).ToList();
    }

    public async Task<InputDocumentResponseDto> GetInputDocumentByCodeAsync(int teacherId, string documentCode)
    {
        var inputDocument = await _unitOfWork.InputDocumentRepository
            .GetInputDocumentByCodeAndTeacherAsync(documentCode, teacherId)
            ?? throw new KeyNotFoundException($"InputDocument '{documentCode}' không tồn tại hoặc không thuộc về bạn");

        return MapToInputDocumentResponse(inputDocument);
    }

    public async Task DeleteInputDocumentAsync(int teacherId, string documentCode)
    {
        var inputDocument = await _unitOfWork.InputDocumentRepository
            .GetInputDocumentByCodeAndTeacherAsync(documentCode, teacherId)
            ?? throw new KeyNotFoundException($"InputDocument '{documentCode}' không tồn tại hoặc không thuộc về bạn");

        var bucketName = _configuration["GCS:BucketName"]
            ?? throw new InvalidOperationException("GCS BucketName not configured");
        var storageClient = await StorageClient.CreateAsync();

        if (!string.IsNullOrWhiteSpace(inputDocument.FilePath))
        {
            var objectName = ExtractObjectName(bucketName, inputDocument.FilePath);
            if (!string.IsNullOrWhiteSpace(objectName))
            {
                try
                {
                    var deleteStart = Stopwatch.GetTimestamp();
                    await storageClient.DeleteObjectAsync(bucketName, objectName);
                    var deleteElapsed = Stopwatch.GetElapsedTime(deleteStart);
                    _logger.LogInformation("GCS delete completed in {ElapsedMs}ms: {GcsPath}", deleteElapsed.TotalMilliseconds, inputDocument.FilePath);
                }
                catch (Google.GoogleApiException exception) when (exception.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("GCS file not found, skipping delete: {GcsPath}", inputDocument.FilePath);
                }
            }
        }

        // Cascade soft-delete: Document → Products → ProductVideos
        var products = await _unitOfWork.PipelineRepository
            .GetActiveProductsWithVideosBySourceInputIdAsync(inputDocument.DocumentId);

        foreach (var product in products)
        {
            foreach (var video in product.ProductVideos.Where(v => v.Status != VideoStatusConstants.Deleted))
            {
                video.Status = VideoStatusConstants.Deleted;
                video.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.PipelineRepository.UpdateProductVideo(video);
            }

            product.Status = ProductStatusConstants.Deleted;
            _unitOfWork.PipelineRepository.UpdateProduct(product);
        }

        _unitOfWork.InputDocumentRepository.DeleteInputDocument(inputDocument);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Teacher {TeacherId} soft-deleted InputDocument {DocumentCode} — cascaded to {ProductCount} product(s)",
            teacherId, documentCode, products.Count);
    }

    private static string ExtractObjectName(string bucketName, string gcsPath)
    {
        var prefix = $"gs://{bucketName}/";
        return gcsPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? gcsPath[prefix.Length..]
            : gcsPath;
    }

    private static InputDocumentResponseDto MapToInputDocumentResponse(InputDocuments document)
    {
        return new InputDocumentResponseDto
        {
            DocumentCode = document.DocumentCode,
            Title = document.Title,
            FilePath = document.FilePath,
            ProjectCode = document.Project?.ProjectCode,
            SubjectCode = document.Subject?.SubjectCode,
            SubjectName = document.Subject?.SubjectName,
            GradeCode = document.Grade?.GradeCode,
            GradeName = document.Grade?.GradeName,
            LessonCode = document.Lesson?.LessonCode,
            LessonName = document.Lesson?.LessonName,
            UploadDate = document.UploadDate
        };
    }
}
