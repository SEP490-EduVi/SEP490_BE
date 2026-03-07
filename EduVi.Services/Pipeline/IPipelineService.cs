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
}
