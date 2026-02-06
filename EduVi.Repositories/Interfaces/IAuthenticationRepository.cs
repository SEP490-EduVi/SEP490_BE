using EduVi.Repositories.Models;

namespace EduVi.Contracts.Repositories;

public interface IAuthenticationRepository
{
    /// <summary>
    /// Tìm người dùng theo UserId
    /// </summary>
    Task<Users?> GetUserByIdAsync(int userId);

    /// <summary>
    /// Tìm người dùng theo email (phục vụ đăng nhập Social - Google hoặc Email)
    /// </summary>
    Task<Users?> GetUserByEmailAsync(string email);

    /// <summary>
    /// Tìm người dùng theo username (phục vụ đăng nhập truyền thống)
    /// </summary>
    Task<Users?> GetUserByUsernameAsync(string username);

    /// <summary>
    /// Lưu người dùng mới khi đăng ký (mặc định trạng thái Active)
    /// </summary>
    Task<Users> CreateUserAsync(Users user);

    /// <summary>
    /// Cập nhật thông tin người dùng (dùng cho verify email, change password, etc.)
    /// </summary>
    Task<Users> UpdateUserAsync(Users user);

    /// <summary>
    /// Cập nhật trạng thái người dùng (Active/Banned) theo lệnh của Admin
    /// </summary>
    Task<bool> UpdateUserStatusAsync(int userId, int status);

    /// <summary>
    /// Lấy thông tin vai trò để định tuyến người dùng sau login
    /// </summary>
    Task<Roles?> GetRoleByIdAsync(int roleId);

    /// <summary>
    /// Kiểm tra email đã tồn tại chưa
    /// </summary>
    Task<bool> EmailExistsAsync(string email);

    /// <summary>
    /// Kiểm tra username đã tồn tại chưa
    /// </summary>
    Task<bool> UsernameExistsAsync(string username);
}
