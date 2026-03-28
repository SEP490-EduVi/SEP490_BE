using EduVi.Contracts.DTOs.Pipeline;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
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
    private readonly ILogger<PipelineService> _logger;

    public PipelineService(
        IUnitOfWork unitOfWork,
        IRabbitMqPublisherService publisher,
        IConnectionMultiplexer redis,
        ILogger<PipelineService> logger)
    {
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _redis = redis;
        _logger = logger;
    }

    #region Lesson Analysis

    public async Task<PipelineTaskResponseDto> CreateLessonAnalysisTaskAsync(int teacherId, LessonAnalysisRequestDto request)
    {
        var taskId = Guid.NewGuid();

        // 1. Validate Project belongs to this Teacher (by Code)
        var project = await _unitOfWork.PipelineRepository
            .GetProjectByCodeAndTeacherAsync(request.ProjectCode, teacherId);
        if (project is null)
            throw new InvalidOperationException("Dự án không tồn tại hoặc không thuộc về bạn");

        // 2. Get InputDocument from DB (by Code)
        var document = await _unitOfWork.InputDocumentRepository
            .GetInputDocumentByCodeAndTeacherAsync(request.DocumentCode, teacherId);
        if (document is null)
            throw new InvalidOperationException("InputDocument không tồn tại hoặc không thuộc về bạn");

        if (document.ProjectId != project.ProjectId)
            throw new InvalidOperationException("Tài liệu đầu vào không thuộc Dự án đã chọn");

        var lessonCode = document.Lesson?.LessonCode
            ?? throw new InvalidOperationException("Lesson chưa có LessonCode");
        var subjectCode = document.Subject?.SubjectCode ?? "Unknown";
        var gradeCode = document.Grade?.GradeCode ?? "Unknown";

        // 4. Reuse existing Product by ProductCode (deterministic key for this project+document pair)
        var productCode = $"prod_{request.ProjectCode}_{request.DocumentCode}";
        var product = await _unitOfWork.PipelineRepository
            .GetProductByCodeAndTeacherAsync(productCode, teacherId);

        if (product is not null)
        {
            if (product.Status == ProductStatusConstants.New || product.Status == ProductStatusConstants.Processing)
                throw new InvalidOperationException("Phân tích bài giảng đang được xử lý. Vui lòng chờ hoàn tất trước khi gửi lại");

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
            lessonCode,
            request.CurriculumYear);
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

    public async Task<PipelineTaskResponseDto> CreateSlideGenerationTaskAsync(int teacherId, SlideGenerationRequestDto request)
    {
        var taskId = Guid.NewGuid();

        // 1. Validate Product belongs to this Teacher and has evaluation data
        var product = await _unitOfWork.PipelineRepository
            .GetProductByCodeAndTeacherAsync(request.ProductCode, teacherId)
            ?? throw new InvalidOperationException("Nội dung số không tồn tại hoặc không thuộc về bạn");

        if (string.IsNullOrEmpty(product.EvaluationResult))
            throw new InvalidOperationException("Nội dung số chưa được đánh giá. Hãy chạy phân tích bài giảng trước");

        if (string.IsNullOrEmpty(product.LessonPlanText))
            throw new InvalidOperationException("Nội dung số thiếu dữ liệu giáo án. Hãy chạy lại phân tích bài giảng");

        if (product.Status == ProductStatusConstants.GeneratingSlides)
            throw new InvalidOperationException("Slide đang được tạo. Vui lòng chờ hoàn tất trước khi gửi lại");

        // 2. Deserialize stored data to pass to the Python worker
        var evaluationResult = JsonSerializer.Deserialize<object>(product.EvaluationResult);

        // 3. Update product status to GeneratingSlides
        product.Status = ProductStatusConstants.GeneratingSlides;
        product.SlideDocument = null;
        product.SlideGeneratedAt = null;
        _unitOfWork.PipelineRepository.UpdateProduct(product);
        await _unitOfWork.SaveChangesAsync();

        // 4. Store task metadata in Redis (TTL 1 hour)
        var database = _redis.GetDatabase();
        var taskMeta = JsonSerializer.Serialize(new
        {
            userId = teacherId.ToString(),
            productId = product.ProductId,
            status = "queued",
            step = "slide_generation",
            createdAt = DateTime.UtcNow.ToString("o")
        });
        var redisStart = Stopwatch.GetTimestamp();
        await database.StringSetAsync($"pipeline:status:{taskId}", taskMeta, TimeSpan.FromHours(1));
        var redisElapsed = Stopwatch.GetElapsedTime(redisStart);
        _logger.LogInformation("Redis slide generation task metadata stored in {ElapsedMs}ms for task {TaskId}", redisElapsed.TotalMilliseconds, taskId);

        // 5. Publish task to RabbitMQ
        var publishStart = Stopwatch.GetTimestamp();
        await _publisher.PublishSlideGenerationTaskAsync(
            taskId,
            teacherId.ToString(),
            product.ProductId,
            evaluationResult!,
            product.LessonPlanText,
            request.SlideRange);
        var publishElapsed = Stopwatch.GetElapsedTime(publishStart);
        _logger.LogInformation("RabbitMQ slide generation task published in {ElapsedMs}ms for task {TaskId}, product {ProductCode}",
            publishElapsed.TotalMilliseconds, taskId, product.ProductCode);

        return new PipelineTaskResponseDto
        {
            TaskId = taskId,
            ProductCode = product.ProductCode,
            Status = "queued"
        };
    }

    public async Task<PipelineTaskResponseDto> CreateVideoGenerationTaskAsync(int teacherId, GenerateVideoRequestDto request)
    {
        var taskId = Guid.NewGuid();

        var product = await _unitOfWork.PipelineRepository
            .GetProductByCodeAndTeacherAsync(request.ProductCode, teacherId)
            ?? throw new InvalidOperationException("Nội dung số không tồn tại hoặc không thuộc về bạn");

        var activeVideo = await _unitOfWork.PipelineRepository.GetLatestActiveProductVideoAsync(product.ProductId);
        if (activeVideo?.Status == VideoStatusConstants.Queued)
            throw new InvalidOperationException("Video đang được tạo. Vui lòng chờ hoàn tất trước khi gửi lại");

        if (!IsSupportedGcsUrl(request.SlideEditedDocumentUrl))
            throw new InvalidOperationException("SlideEditedDocumentUrl phải là GCS URL hợp lệ (gs://... hoặc https://storage.googleapis.com/...)");

        var slideEditedDocumentUrl = request.SlideEditedDocumentUrl.Trim();

        product.SlideEditedDocument = slideEditedDocumentUrl;
        product.SlideEditedAt = DateTime.UtcNow;

        _unitOfWork.PipelineRepository.UpdateProduct(product);

        var productVideoCode = $"video_{product.ProductCode}_{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        await _unitOfWork.PipelineRepository.CreateProductVideoAsync(new ProductVideos
        {
            ProductId = product.ProductId,
            ProductVideoCode = productVideoCode,
            Status = VideoStatusConstants.Queued,
            SlideDocumentUrl = slideEditedDocumentUrl,
            CreatedAt = DateTime.UtcNow
        });
        await _unitOfWork.SaveChangesAsync();

        var db = _redis.GetDatabase();
        var taskMeta = JsonSerializer.Serialize(new
        {
            userId = teacherId.ToString(),
            productId = product.ProductId,
            productVideoCode,
            status = "queued",
            step = "video_generation",
            createdAt = DateTime.UtcNow.ToString("o")
        });
        var redisStart = Stopwatch.GetTimestamp();
        await db.StringSetAsync($"pipeline:status:{taskId}", taskMeta, TimeSpan.FromHours(1));
        var redisElapsed = Stopwatch.GetElapsedTime(redisStart);
        _logger.LogInformation("Redis video generation task metadata stored in {ElapsedMs}ms for task {TaskId}",
            redisElapsed.TotalMilliseconds, taskId);

        var publishStart = Stopwatch.GetTimestamp();
        await _publisher.PublishVideoGenerationTaskAsync(
            taskId,
            productVideoCode,
            teacherId.ToString(),
            product.ProductId,
            product.ProductCode,
            slideEditedDocumentUrl);
        var publishElapsed = Stopwatch.GetElapsedTime(publishStart);
        _logger.LogInformation("RabbitMQ video generation task published in {ElapsedMs}ms for task {TaskId}, product {ProductCode}",
            publishElapsed.TotalMilliseconds, taskId, product.ProductCode);

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

    #region Slide Edit

    public async Task<string> SaveEditedSlideAsync(int teacherId, string productCode, SaveEditedSlideRequestDto request)
    {
        var product = await _unitOfWork.PipelineRepository
            .GetProductByCodeAndTeacherAsync(productCode, teacherId)
            ?? throw new KeyNotFoundException($"Không tìm thấy nội dung số với mã {productCode}");

        if (string.IsNullOrEmpty(product.SlideDocument))
            throw new InvalidOperationException("Nội dung số chưa có slide. Hãy tạo slide trước khi chỉnh sửa");

        // Validate và upsert ProductComponent nếu có materials
        if (request.UsedMaterials?.Count > 0)
        {
            var newComponents = new List<ProductComponent>();

            foreach (var usedMaterial in request.UsedMaterials)
            {
                // Resolve MaterialCode → MaterialId
                var materialId = await _unitOfWork.PipelineRepository
                    .GetMaterialIdByCodeAsync(usedMaterial.MaterialCode)
                    ?? throw new InvalidOperationException($"Không tìm thấy học liệu với mã '{usedMaterial.MaterialCode}'");

                // Kiểm tra Teacher đã mua material này chưa
                var isOwned = await _unitOfWork.PipelineRepository
                    .IsTeacherOwnsMaterialAsync(teacherId, materialId);
                if (!isOwned)
                    throw new InvalidOperationException($"Bạn chưa sở hữu học liệu '{usedMaterial.MaterialCode}'");

                newComponents.Add(new ProductComponent
                {
                    ProductId = product.ProductId,
                    MaterialId = materialId,
                    TeacherId = teacherId,
                    ComponentCode = $"COMP_{product.ProductCode}_{usedMaterial.BlockId}",
                    CardId = usedMaterial.CardId,
                    BlockId = usedMaterial.BlockId,
                    AddedAt = DateTime.UtcNow
                });
            }

            // Upsert: xóa cũ, thêm mới
            var existingComponents = await _unitOfWork.PipelineRepository
                .GetProductComponentsAsync(product.ProductId);
            _unitOfWork.PipelineRepository.DeleteProductComponents(existingComponents);
            await _unitOfWork.PipelineRepository.AddProductComponentsAsync(newComponents);
        }
        else
        {
            // Không còn material nào → xóa hết components cũ
            var existingComponents = await _unitOfWork.PipelineRepository
                .GetProductComponentsAsync(product.ProductId);
            if (existingComponents.Count > 0)
                _unitOfWork.PipelineRepository.DeleteProductComponents(existingComponents);
        }

        if (!IsSupportedGcsUrl(request.SlideEditedDocumentUrl))
            throw new InvalidOperationException("SlideEditedDocumentUrl phải là GCS URL hợp lệ (gs://... hoặc https://storage.googleapis.com/...)");

        var slideEditedDocumentUrl = request.SlideEditedDocumentUrl.Trim();

        // Lưu link slide đã edit — bản gốc SlideDocument giữ nguyên
        product.SlideEditedDocument = slideEditedDocumentUrl;
        product.SlideEditedAt = DateTime.UtcNow;

        _unitOfWork.PipelineRepository.UpdateProduct(product);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Teacher {TeacherId} saved edited slide for product {ProductCode} with {MaterialCount} material(s) at {EditedAt}",
            teacherId, productCode, request.UsedMaterials?.Count ?? 0, product.SlideEditedAt);

        return slideEditedDocumentUrl;
    }

    private static bool IsSupportedGcsUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.StartsWith("gs://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://storage.googleapis.com/", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Product Queries

    public async Task<List<ProductSummaryDto>> GetProductsByTeacherAsync(int teacherId)
    {
        var products = await _unitOfWork.PipelineRepository.GetProductsByTeacherAsync(teacherId);

        return products.Select(product => new ProductSummaryDto
        {
            ProductCode = product.ProductCode,
            ProductName = product.ProductName,
            Description = product.Description,
            Status = product.Status ?? 0,
            StatusName = ProductStatusConstants.GetStatusName(product.Status),
            EvaluatedAt = product.EvaluatedAt,
            SlideGeneratedAt = product.SlideGeneratedAt,
            SlideEditedAt = product.SlideEditedAt,
            HasEvaluation = !string.IsNullOrEmpty(product.EvaluationResult),
            HasSlide = !string.IsNullOrEmpty(product.SlideDocument),
            HasEditedSlide = !string.IsNullOrEmpty(product.SlideEditedDocument)
        }).ToList();
    }

    public async Task<List<ProductSummaryDto>> GetProductsByProjectCodeAsync(int teacherId, string projectCode)
    {
        var project = await _unitOfWork.PipelineRepository
            .GetProjectByCodeAndTeacherAsync(projectCode, teacherId)
            ?? throw new KeyNotFoundException($"Dự án '{projectCode}' không tồn tại hoặc không thuộc về bạn");

        var products = await _unitOfWork.PipelineRepository
            .GetProductsByTeacherAndProjectAsync(teacherId, project.ProjectId);

        return products.Select(product => new ProductSummaryDto
        {
            ProductCode = product.ProductCode,
            ProductName = product.ProductName,
            Description = product.Description,
            Status = product.Status ?? 0,
            StatusName = ProductStatusConstants.GetStatusName(product.Status),
            EvaluatedAt = product.EvaluatedAt,
            SlideGeneratedAt = product.SlideGeneratedAt,
            SlideEditedAt = product.SlideEditedAt,
            HasEvaluation = !string.IsNullOrEmpty(product.EvaluationResult),
            HasSlide = !string.IsNullOrEmpty(product.SlideDocument),
            HasEditedSlide = !string.IsNullOrEmpty(product.SlideEditedDocument)
        }).ToList();
    }

    public async Task<ProductDetailDto> GetProductByCodeAsync(int teacherId, string productCode)
    {
        var product = await _unitOfWork.PipelineRepository
            .GetProductByCodeAndTeacherAsync(productCode, teacherId)
            ?? throw new KeyNotFoundException($"Không tìm thấy nội dung số với mã {productCode}");

        if (product.Status == ProductStatusConstants.Deleted)
            throw new KeyNotFoundException($"Không tìm thấy nội dung số với mã {productCode}");

        var latestProductVideo = await _unitOfWork.PipelineRepository.GetLatestActiveProductVideoAsync(product.ProductId);

        return new ProductDetailDto
        {
            ProductCode = product.ProductCode,
            ProductName = product.ProductName,
            Description = product.Description,
            Status = product.Status ?? 0,
            StatusName = ProductStatusConstants.GetStatusName(product.Status),
            EvaluationResult = ParseJson(product.EvaluationResult),
            EvaluatedAt = product.EvaluatedAt,
            LessonPlanText = product.LessonPlanText,
            SlideDocument = ParseJson(product.SlideDocument),
            SlideGeneratedAt = product.SlideGeneratedAt,
            SlideEditedDocument = ParseJson(product.SlideEditedDocument),
            SlideEditedAt = product.SlideEditedAt,
            VideoUrl = latestProductVideo?.VideoUrl,
            VideoDuration = latestProductVideo?.Duration,
            ProductVideoCode = latestProductVideo?.ProductVideoCode,
            VideoInteractions = ParseJson(latestProductVideo?.Interactions),
            VideoPausePoints = ParseJson(latestProductVideo?.PausePoints),
            VideoGeneratedAt = latestProductVideo?.CompletedAt
        };
    }

    public async Task<ProductVideoDetailDto> GetLatestProductVideoByProjectCodeAsync(int teacherId, string projectCode)
    {
        var productVideo = await _unitOfWork.PipelineRepository
            .GetLatestActiveProductVideoByProjectCodeAndTeacherAsync(projectCode, teacherId)
            ?? throw new KeyNotFoundException($"Dự án {projectCode} chưa có video nào");

        return MapToProductVideoDetailDto(productVideo);
    }

    public async Task<List<ProductVideoDetailDto>> GetProductVideosByTeacherAsync(int teacherId)
    {
        var productVideos = await _unitOfWork.PipelineRepository
            .GetActiveProductVideosByTeacherAsync(teacherId);

        return productVideos
            .Select(MapToProductVideoDetailDto)
            .ToList();
    }

    public async Task<List<ProductVideoDetailDto>> GetProductVideosByProjectCodeAsync(int teacherId, string projectCode)
    {
        var project = await _unitOfWork.PipelineRepository
            .GetProjectByCodeAndTeacherAsync(projectCode, teacherId)
            ?? throw new KeyNotFoundException($"Dự án {projectCode} không tồn tại hoặc không thuộc về bạn");

        var productVideos = await _unitOfWork.PipelineRepository
            .GetActiveProductVideosByProjectCodeAndTeacherAsync(project.ProjectCode, teacherId);

        return productVideos
            .Select(MapToProductVideoDetailDto)
            .ToList();
    }

    public async Task<ProductVideoDetailDto> GetLatestProductVideoByProductCodeAsync(int teacherId, string productCode)
    {
        var product = await _unitOfWork.PipelineRepository
            .GetProductByCodeAndTeacherAsync(productCode, teacherId)
            ?? throw new KeyNotFoundException($"Không tìm thấy nội dung số với mã {productCode}");

        var productVideo = await _unitOfWork.PipelineRepository
            .GetLatestActiveProductVideoAsync(product.ProductId)
            ?? throw new KeyNotFoundException($"Nội dung số {productCode} chưa có video nào");

        if (productVideo.Product is null)
            productVideo.Product = product;

        return MapToProductVideoDetailDto(productVideo);
    }

    public async Task SoftDeleteProductVideoAsync(int teacherId, string productVideoCode)
    {
        var productVideo = await _unitOfWork.PipelineRepository
            .GetProductVideoByCodeAndTeacherAsync(productVideoCode, teacherId)
            ?? throw new KeyNotFoundException($"Không tìm thấy video với mã {productVideoCode}");

        if (productVideo.Status == VideoStatusConstants.Deleted)
            throw new KeyNotFoundException($"Không tìm thấy video với mã {productVideoCode}");

        productVideo.Status = VideoStatusConstants.Deleted;
        productVideo.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.PipelineRepository.UpdateProductVideo(productVideo);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Teacher {TeacherId} soft-deleted product video {ProductVideoCode}", teacherId, productVideoCode);
    }

    private ProductVideoDetailDto MapToProductVideoDetailDto(ProductVideos productVideo)
    {
        return new ProductVideoDetailDto
        {
            ProductCode = productVideo.Product?.ProductCode ?? string.Empty,
            ProductName = productVideo.Product?.ProductName ?? string.Empty,
            ProductVideoCode = productVideo.ProductVideoCode,
            Status = VideoStatusConstants.GetStatusName(productVideo.Status),
            SlideDocumentUrl = productVideo.SlideDocumentUrl,
            VideoUrl = productVideo.VideoUrl,
            Duration = productVideo.Duration,
            Interactions = ParseJson(productVideo.Interactions),
            PausePoints = ParseJson(productVideo.PausePoints),
            ErrorMessage = productVideo.ErrorMessage,
            CreatedAt = productVideo.CreatedAt,
            UpdatedAt = productVideo.UpdatedAt,
            CompletedAt = productVideo.CompletedAt
        };
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

    #endregion

    #region Product Delete

    public async Task DeleteProductAsync(int teacherId, string productCode)
    {
        var product = await _unitOfWork.PipelineRepository
            .GetProductByCodeAndTeacherAsync(productCode, teacherId)
            ?? throw new KeyNotFoundException($"Không tìm thấy nội dung số với mã {productCode}");

        if (product.Status == ProductStatusConstants.Deleted)
            throw new KeyNotFoundException($"Không tìm thấy nội dung số với mã {productCode}");

        if (product.Status == ProductStatusConstants.Processing ||
            product.Status == ProductStatusConstants.GeneratingSlides)
            throw new InvalidOperationException("Không thể xóa nội dung số đang được xử lý. Vui lòng chờ hoàn tất");

        product.Status = ProductStatusConstants.Deleted;
        _unitOfWork.PipelineRepository.UpdateProduct(product);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Teacher {TeacherId} soft-deleted product {ProductCode}", teacherId, productCode);
    }

    #endregion
}
