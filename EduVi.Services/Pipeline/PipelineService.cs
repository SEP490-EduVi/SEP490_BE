using EduVi.Contracts.DTOs.Pipeline;
using EduVi.Contracts.DTOs.Project;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Diagnostics;
using System.Text.Json;

namespace EduVi.Services.Pipeline;

public class PipelineService : IPipelineService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRabbitMqPublisherService _publisher;
    private readonly IConnectionMultiplexer _redis;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PipelineService> _logger;

    public PipelineService(
        IUnitOfWork unitOfWork,
        IRabbitMqPublisherService publisher,
        IConnectionMultiplexer redis,
        IConfiguration configuration,
        ILogger<PipelineService> logger)
    {
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _redis = redis;
        _configuration = configuration;
        _logger = logger;
    }

    #region Input Documents

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

    #endregion

    #region Projects

    public async Task<List<ProjectResponseDto>> GetProjectsByTeacherAsync(int teacherId)
    {
        var projects = await _unitOfWork.PipelineRepository.GetProjectsByTeacherAsync(teacherId);
        return projects.Select(MapToProjectResponse).ToList();
    }

    public async Task<ProjectResponseDto> GetProjectByCodeAsync(string projectCode)
    {
        var project = await _unitOfWork.PipelineRepository.GetProjectByCodeAsync(projectCode, includeRelations: true)
            ?? throw new KeyNotFoundException($"Project '{projectCode}' không tồn tại");

        return MapToProjectResponse(project);
    }

    public async Task<ProjectResponseDto> CreateProjectAsync(int teacherId, CreateProjectRequestDto request)
    {
        var existing = await _unitOfWork.PipelineRepository.GetProjectByCodeAsync(request.ProjectCode);
        if (existing is not null)
            throw new InvalidOperationException($"ProjectCode '{request.ProjectCode}' đã tồn tại");

        var project = new Projects
        {
            TeacherId = teacherId,
            ProjectCode = request.ProjectCode,
            ProjectName = request.ProjectName,
            Status = 0
        };

        await _unitOfWork.PipelineRepository.CreateProjectAsync(project);
        await _unitOfWork.SaveChangesAsync();

        var saved = await _unitOfWork.PipelineRepository.GetProjectByCodeAsync(project.ProjectCode, includeRelations: true);
        return MapToProjectResponse(saved!);
    }

    public async Task<ProjectResponseDto> UpdateProjectAsync(int teacherId, string projectCode, UpdateProjectRequestDto request)
    {
        var project = await _unitOfWork.PipelineRepository.GetProjectByCodeAndTeacherAsync(projectCode, teacherId)
            ?? throw new KeyNotFoundException($"Project '{projectCode}' không tồn tại hoặc không thuộc về bạn");

        if (request.ProjectCode is not null && request.ProjectCode != projectCode)
        {
            var existing = await _unitOfWork.PipelineRepository.GetProjectByCodeAsync(request.ProjectCode);
            if (existing is not null)
                throw new InvalidOperationException($"ProjectCode '{request.ProjectCode}' đã được sử dụng");
            project.ProjectCode = request.ProjectCode;
        }

        if (request.ProjectName is not null)
            project.ProjectName = request.ProjectName;

        if (request.Status.HasValue)
            project.Status = request.Status.Value;

        _unitOfWork.PipelineRepository.UpdateProject(project);
        await _unitOfWork.SaveChangesAsync();

        var saved = await _unitOfWork.PipelineRepository.GetProjectByCodeAsync(project.ProjectCode, includeRelations: true);
        return MapToProjectResponse(saved!);
    }

    public async Task DeleteProjectAsync(int teacherId, string projectCode)
    {
        var project = await _unitOfWork.PipelineRepository.GetProjectByCodeAsync(projectCode, includeRelations: true)
            ?? throw new KeyNotFoundException($"Project '{projectCode}' không tồn tại");

        if (project.TeacherId != teacherId)
            throw new InvalidOperationException("Project không thuộc về bạn");

        if (project.Products.Count > 0)
            throw new InvalidOperationException($"Không thể xóa Project đang có {project.Products.Count} Product");

        _unitOfWork.PipelineRepository.DeleteProject(project);
        await _unitOfWork.SaveChangesAsync();
    }

    #endregion

    #region Lesson Analysis

    public async Task<PipelineTaskResponseDto> CreateLessonAnalysisTaskAsync(int teacherId, LessonAnalysisRequestDto request)
    {
        var taskId = Guid.NewGuid();

        // 1. Validate Project belongs to this Teacher (by Code)
        var project = await _unitOfWork.PipelineRepository
            .GetProjectByCodeAndTeacherAsync(request.ProjectCode, teacherId);
        if (project is null)
            throw new InvalidOperationException("Project không tồn tại hoặc không thuộc về bạn");

        // 2. Get InputDocument from DB (by Code)
        var document = await _unitOfWork.PipelineRepository
            .GetInputDocumentByCodeAsync(request.DocumentCode);
        if (document is null)
            throw new InvalidOperationException("InputDocument không tồn tại");

        // 3. Verify document belongs to this Teacher
        if (document.TeacherId != teacherId)
            throw new InvalidOperationException("Document không thuộc về bạn");

        var lessonCode = document.Lesson?.LessonCode
            ?? throw new InvalidOperationException("Lesson chưa có LessonCode");
        var subjectCode = document.Subject?.SubjectCode ?? "Unknown";
        var gradeCode = document.Grade?.GradeCode ?? "Unknown";

        // 4. Reuse existing Product for same project + document, or create new
        var product = await _unitOfWork.PipelineRepository
            .GetExistingProductAsync(project.ProjectId, document.DocumentId);

        var productCode = $"prod_{request.ProjectCode}_{request.DocumentCode}";

        if (product is not null)
        {
            product.ProductName = request.ProductName ?? $"Phân tích: {document.Title}";
            product.Description = $"AI evaluation cho document: {document.Title}";
            product.ProductCode = productCode;
            product.Status = ProductStatusConstants.New;
            product.EvaluationResult = null;
            product.EvaluatedAt = null;
            _unitOfWork.PipelineRepository.UpdateProduct(product);
        }
        else
        {
            product = new Products
            {
                ProjectId = project.ProjectId,
                TeacherId = teacherId,
                ProductCode = productCode,
                ProductName = request.ProductName ?? $"Phân tích: {document.Title}",
                Description = $"AI evaluation cho document: {document.Title}",
                SourceInputId = document.DocumentId,
                Status = ProductStatusConstants.New,
                Price = 0
            };
            await _unitOfWork.PipelineRepository.CreateProductAsync(product);
        }

        await _unitOfWork.SaveChangesAsync();

        // 5. Store task metadata in Redis (TTL 1 hour)
        var db = _redis.GetDatabase();
        var taskMeta = JsonSerializer.Serialize(new
        {
            userId = teacherId.ToString(),
            productId = product.ProductId,
            status = "queued",
            subjectCode,
            gradeCode,
            lessonCode,
            gcsUri = document.FilePath,
            createdAt = DateTime.UtcNow.ToString("o")
        });
        var redisStart = Stopwatch.GetTimestamp();
        await db.StringSetAsync($"pipeline:status:{taskId}", taskMeta, TimeSpan.FromHours(1));
        var redisElapsed = Stopwatch.GetElapsedTime(redisStart);
        _logger.LogInformation("Redis task metadata stored in {ElapsedMs}ms for task {TaskId}", redisElapsed.TotalMilliseconds, taskId);

        // 6. Publish task to RabbitMQ (matches Python format)
        var publishStart = Stopwatch.GetTimestamp();
        await _publisher.PublishLessonAnalysisTaskAsync(
            taskId,
            teacherId.ToString(),
            product.ProductId,
            document.FilePath,
            subjectCode,
            gradeCode,
            lessonCode);
        var publishElapsed = Stopwatch.GetElapsedTime(publishStart);
        _logger.LogInformation("RabbitMQ lesson analysis task published in {ElapsedMs}ms for task {TaskId}, product {ProductCode}",
            publishElapsed.TotalMilliseconds, taskId, productCode);

        return new PipelineTaskResponseDto
        {
            TaskId = taskId,
            ProductCode = product.ProductCode,
            Status = "queued"
        };
    }

    public async Task<PipelineProgressDto?> GetTaskStatusAsync(Guid taskId)
    {
        var db = _redis.GetDatabase();
        var data = await db.StringGetAsync($"pipeline:status:{taskId}");

        if (data.IsNullOrEmpty)
            return null;

        return JsonSerializer.Deserialize<PipelineProgressDto>(data!, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    #endregion

    #region Mapping Helpers

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

    private static ProjectResponseDto MapToProjectResponse(Projects project)
    {
        return new ProjectResponseDto
        {
            ProjectCode = project.ProjectCode,
            ProjectName = project.ProjectName,
            Status = project.Status
        };
    }

    #endregion
}
