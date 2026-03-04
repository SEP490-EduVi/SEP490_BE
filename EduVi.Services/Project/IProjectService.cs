using EduVi.Contracts.DTOs.Project;

namespace EduVi.Services.Project;

public interface IProjectService
{
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
}
