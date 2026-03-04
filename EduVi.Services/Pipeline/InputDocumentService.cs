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
        // 1. Resolve codes to internal IDs first (needed for file naming)
        var subject = await _unitOfWork.CurriculumRepository.GetSubjectByCodeAsync(request.SubjectCode)
            ?? throw new InvalidOperationException($"Subject '{request.SubjectCode}' không tồn tại");
        var grade = await _unitOfWork.CurriculumRepository.GetGradeByCodeAsync(request.GradeCode)
            ?? throw new InvalidOperationException($"Grade '{request.GradeCode}' không tồn tại");

        int? lessonId = null;
        string lessonPart = "";
        if (request.LessonCode is not null)
        {
            var lesson = await _unitOfWork.CurriculumRepository.GetLessonByCodeAsync(request.LessonCode)
                ?? throw new InvalidOperationException($"Lesson '{request.LessonCode}' không tồn tại");
            lessonId = lesson.LessonId;
            lessonPart = $"_{request.LessonCode}";
        }

        // 2. Build meaningful filename and upload to GCS
        var bucketName = _configuration["GCS:BucketName"]
            ?? throw new InvalidOperationException("GCS BucketName not configured");

        var storageClient = await StorageClient.CreateAsync();

        // 3. Check if an InputDocument already exists for this teacher + subject + grade + lesson
        var existing = await _unitOfWork.PipelineRepository
            .GetExistingInputDocumentAsync(teacherId, subject.SubjectId, grade.GradeId, lessonId);

        if (existing is not null && !string.IsNullOrEmpty(existing.FilePath))
        {
            // Delete old file from GCS (gs://bucket/object → extract object name)
            var oldObjectName = existing.FilePath
                .Replace($"gs://{bucketName}/", "");
            try
            {
                var deleteStart = Stopwatch.GetTimestamp();
                await storageClient.DeleteObjectAsync(bucketName, oldObjectName);
                var deleteElapsed = Stopwatch.GetElapsedTime(deleteStart);
                _logger.LogInformation("GCS delete completed in {ElapsedMs}ms: {OldPath}", deleteElapsed.TotalMilliseconds, existing.FilePath);
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Old GCS file not found, skipping delete: {OldPath}", existing.FilePath);
            }
        }

        var fileExtension = Path.GetExtension(request.File.FileName);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var fileName = $"{request.SubjectCode}_{request.GradeCode}{lessonPart}_{timestamp}{fileExtension}";
        var objectName = $"user_inputs/{teacherId}/{fileName}";

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

        // 4. Update existing or create new InputDocument in DB
        var documentCode = $"doc_{request.SubjectCode}_{request.GradeCode}{lessonPart}_{timestamp}";

        InputDocuments document;
        if (existing is not null)
        {
            existing.Title = request.Title;
            existing.FilePath = gcsPath;
            existing.DocumentCode = documentCode;
            existing.UploadDate = DateTime.UtcNow;
            _unitOfWork.PipelineRepository.UpdateInputDocument(existing);
            document = existing;
        }
        else
        {
            document = new InputDocuments
            {
                TeacherId = teacherId,
                Title = request.Title,
                DocumentCode = documentCode,
                SubjectId = subject.SubjectId,
                GradeId = grade.GradeId,
                LessonId = lessonId,
                FilePath = gcsPath,
                UploadDate = DateTime.UtcNow
            };
            await _unitOfWork.PipelineRepository.CreateInputDocumentAsync(document);
        }

        await _unitOfWork.SaveChangesAsync();

        // 5. Reload with navigation properties for response
        var saved = await _unitOfWork.PipelineRepository
            .GetInputDocumentByIdAsync(document.DocumentId);

        return MapToInputDocumentResponse(saved!);
    }

    public async Task<List<InputDocumentResponseDto>> GetInputDocumentsByTeacherAsync(int teacherId)
    {
        var documents = await _unitOfWork.PipelineRepository
            .GetInputDocumentsByTeacherAsync(teacherId);

        return documents.Select(MapToInputDocumentResponse).ToList();
    }

    private static InputDocumentResponseDto MapToInputDocumentResponse(InputDocuments doc)
    {
        return new InputDocumentResponseDto
        {
            DocumentCode = doc.DocumentCode,
            Title = doc.Title,
            FilePath = doc.FilePath,
            SubjectCode = doc.Subject?.SubjectCode,
            SubjectName = doc.Subject?.SubjectName,
            GradeCode = doc.Grade?.GradeCode,
            GradeName = doc.Grade?.GradeName,
            LessonCode = doc.Lesson?.LessonCode,
            LessonName = doc.Lesson?.LessonName,
            UploadDate = doc.UploadDate
        };
    }
}
