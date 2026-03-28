using EduVi.Contracts.DTOs.Pipeline;

namespace EduVi.Services.Pipeline;

public interface IPipelineService
{
    /// <summary>
    /// Tạo Product (NEW) + publish task phân tích bài giảng lên RabbitMQ
    /// </summary>
    Task<PipelineTaskResponseDto> CreateLessonAnalysisTaskAsync(int teacherId, LessonAnalysisRequestDto request);

    /// <summary>
    /// Trigger tạo slide presentation từ evaluation result đã có
    /// </summary>
    Task<PipelineTaskResponseDto> CreateSlideGenerationTaskAsync(int teacherId, SlideGenerationRequestDto request);
    Task<PipelineTaskResponseDto> CreateVideoGenerationTaskAsync(int teacherId, GenerateVideoRequestDto request);

    /// <summary>
    /// Lấy trạng thái task từ Redis (fallback khi SignalR bị ngắt)
    /// </summary>
    Task<PipelineProgressDto?> GetTaskStatusAsync(Guid taskId);

    /// <summary>
    /// Teacher lưu slide đã chỉnh sửa vào SlideEditedDocument
    /// </summary>
    Task<string> SaveEditedSlideAsync(int teacherId, string productCode, SaveEditedSlideRequestDto request);

    // Product queries
    Task<List<ProductSummaryDto>> GetProductsByTeacherAsync(int teacherId);
    Task<List<ProductSummaryDto>> GetProductsByProjectCodeAsync(int teacherId, string projectCode);
    Task<ProductDetailDto> GetProductByCodeAsync(int teacherId, string productCode);

    // Video queries
    Task<List<ProductVideoDetailDto>> GetProductVideosByTeacherAsync(int teacherId);
    Task<List<ProductVideoDetailDto>> GetProductVideosByProjectCodeAsync(int teacherId, string projectCode);
    Task<ProductVideoDetailDto> GetLatestProductVideoByProjectCodeAsync(int teacherId, string projectCode);
    Task<ProductVideoDetailDto> GetLatestProductVideoByProductCodeAsync(int teacherId, string productCode);
    Task SoftDeleteProductVideoAsync(int teacherId, string productVideoCode);

    // Product delete
    Task DeleteProductAsync(int teacherId, string productCode);
}
