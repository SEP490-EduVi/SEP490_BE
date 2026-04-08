using EduVi.Contracts.DTOs.Profile;

namespace EduVi.Services.Staff;

public interface IStaffProfileService
{
    /// <summary>
    /// Lấy thông tin profile của Staff đang đăng nhập.
    /// </summary>
    Task<StaffProfileResponse> GetProfileAsync(int userId);

    /// <summary>
    /// Cập nhật thông tin profile (FullName, PhoneNumber, Department).
    /// </summary>
    Task UpdateProfileAsync(int userId, UpdateStaffProfileRequest request);
}
