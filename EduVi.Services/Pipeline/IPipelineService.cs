using EduVi.Contracts.DTOs.Pipeline;
using EduVi.Contracts.DTOs.Project;

namespace EduVi.Services.Pipeline;

public interface IPipelineService
{
    // ============ Input Documents ============

    /// <summary>
    /// Upload file lên GCS và lưu metadata vào DB (InputDocuments)
    /// </summary>
    Task<InputDocumentResponseDto> UploadInputDocumentAsync(int teacherId, UploadInputDocumentRequestDto request);

    /// <summary>
    /// Lấy danh sách InputDocuments của một Teacher
    /// </summary>
    Task<List<InputDocumentResponseDto>> GetInputDocumentsByTeacherAsync(int teacherId);

    // ============ Projects ============

    /// <summary>
    /// Lấy tất cả Projects của một Teacher
    /// </summary>
    Task<List<ProjectResponseDto>> GetProjectsByTeacherAsync(int teacherId);

    /// <summary>
    /// Lấy Project theo Code
    /// </summary>
    Task<ProjectResponseDto> GetProjectByCodeAsync(string projectCode);

    /// <summary>
    /// Tạo Project mới
    /// </summary>
    Task<ProjectResponseDto> CreateProjectAsync(int teacherId, CreateProjectRequestDto request);

    /// <summary>
    /// Cập nhật Project
    /// </summary>
    Task<ProjectResponseDto> UpdateProjectAsync(int teacherId, string projectCode, UpdateProjectRequestDto request);

    /// <summary>
    /// Xóa Project
    /// </summary>
    Task DeleteProjectAsync(int teacherId, string projectCode);

    // ============ Lesson Analysis ============

    /// <summary>
    /// Tạo Product (NEW) + publish task phân tích bài giảng lên RabbitMQ
    /// </summary>
    Task<PipelineTaskResponseDto> CreateLessonAnalysisTaskAsync(int teacherId, LessonAnalysisRequestDto request);

    /// <summary>
    /// Lấy trạng thái task từ Redis (fallback khi SignalR bị ngắt)
    /// </summary>
    Task<PipelineProgressDto?> GetTaskStatusAsync(Guid taskId);
}
