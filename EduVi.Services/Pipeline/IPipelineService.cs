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

    /// <summary>
    /// Lấy trạng thái task từ Redis (fallback khi SignalR bị ngắt)
    /// </summary>
    Task<PipelineProgressDto?> GetTaskStatusAsync(Guid taskId);

    /// <summary>
    /// Teacher lưu slide đã chỉnh sửa vào SlideEditedDocument
    /// </summary>
    Task SaveEditedSlideAsync(int teacherId, string productCode, SaveEditedSlideRequestDto request);

    /// <summary>
    /// Xóa Product (và toàn bộ ProductComponents liên quan) theo ProductCode.
    /// Chỉ xóa được khi product không đang trong trạng thái xử lý.
    /// </summary>
    Task DeleteProductAsync(int teacherId, string productCode);

    /// <summary>
    /// Lấy danh sách Products của Teacher (không bao gồm Deleted)
    /// </summary>
    Task<List<ProductSummaryDto>> GetProductsByTeacherAsync(int teacherId);

    /// <summary>
    /// Lấy danh sách Products của Teacher theo ProjectCode (không bao gồm Deleted)
    /// </summary>
    Task<List<ProductSummaryDto>> GetProductsByProjectCodeAsync(int teacherId, string projectCode);

    /// <summary>
    /// Lấy chi tiết đầy đủ của một Product theo ProductCode
    /// </summary>
    Task<ProductDetailDto> GetProductByCodeAsync(int teacherId, string productCode);
}
