using EduVi.Contracts.DTOs.Profile;

namespace EduVi.Services.Teacher;

public interface ITeacherService
{
    /// <summary>
    /// Lấy thông tin profile của Teacher đang đăng nhập.
    /// </summary>
    Task<TeacherProfileResponse> GetProfileAsync(int userId);

    /// <summary>
    /// Cập nhật thông tin profile (FullName, PhoneNumber, SchoolName).
    /// </summary>
    Task UpdateProfileAsync(int userId, UpdateTeacherProfileRequest request);
}
