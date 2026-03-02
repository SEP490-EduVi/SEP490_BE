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

    /// <summary>
    /// Tạo Admin record với auto-generated AdminCode
    /// </summary>
    Task<Admins> CreateAdminAsync(int userId);

    /// <summary>
    /// Tạo Expert record với auto-generated ExpertCode
    /// </summary>
    Task<Experts> CreateExpertAsync(int userId);

    /// <summary>
    /// Tạo Teacher record với auto-generated TeacherCode
    /// </summary>
    Task<Teachers> CreateTeacherAsync(int userId);

    /// <summary>
    /// Tạo Staff record với auto-generated StaffCode
    /// </summary>
    Task<Staffs> CreateStaffAsync(int userId);

    /// <summary>
    /// Generate unique code for role-specific table
    /// </summary>
    Task<string> GenerateUniqueCodeAsync(string prefix);
}
