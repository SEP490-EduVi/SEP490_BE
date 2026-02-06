using EduVi.Contracts.Repositories;
using EduVi.Repositories.DBContext;
using EduVi.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace EduVi.Repositories.Repositories;

public class AuthenticationRepository : IAuthenticationRepository
{
    private readonly EduViContext _context;

    public AuthenticationRepository(EduViContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Tìm người dùng theo UserId
    /// Bao gồm thông tin Role và các thông tin mở rộng (Admin, Expert, Staff, Teacher)
    /// </summary>
    public async Task<Users?> GetUserByIdAsync(int userId)
    {
        return await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Admins)
            .Include(u => u.Experts)
            .Include(u => u.Staffs)
            .Include(u => u.Teachers)
            .FirstOrDefaultAsync(u => u.UserId == userId);
    }

    /// <summary>
    /// Tìm người dùng theo email (phục vụ đăng nhập Social - Google hoặc Email)
    /// Bao gồm thông tin Role và các thông tin mở rộng (Admin, Expert, Staff, Teacher)
    /// </summary>
    public async Task<Users?> GetUserByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        return await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Admins)
            .Include(u => u.Experts)
            .Include(u => u.Staffs)
            .Include(u => u.Teachers)
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    /// <summary>
    /// Tìm người dùng theo username (phục vụ đăng nhập truyền thống)
    /// Bao gồm thông tin Role và các thông tin mở rộng
    /// </summary>
    public async Task<Users?> GetUserByUsernameAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        return await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Admins)
            .Include(u => u.Experts)
            .Include(u => u.Staffs)
            .Include(u => u.Teachers)
            .FirstOrDefaultAsync(u => u.Username == username);
    }

    /// <summary>
    /// Lưu người dùng mới khi đăng ký
    /// Mặc định trạng thái Active (Status = 1)
    /// </summary>
    public async Task<Users> CreateUserAsync(Users user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        // Đảm bảo trạng thái mặc định là Active
        if (!user.Status.HasValue)
            user.Status = 1; // Active

        // Đảm bảo CreatedAt được set
        if (!user.CreatedAt.HasValue)
            user.CreatedAt = DateTime.UtcNow;

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    /// <summary>
    /// Cập nhật thông tin người dùng (dùng cho verify email, change password, etc.)
    /// </summary>
    public async Task<Users> UpdateUserAsync(Users user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        return user;
    }

    /// <summary>
    /// Cập nhật trạng thái người dùng (Active/Banned) theo lệnh của Admin
    /// Status: 1 = Active, 0 = Banned
    /// </summary>
    public async Task<bool> UpdateUserStatusAsync(int userId, int status)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return false;

        user.Status = status;
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Lấy thông tin vai trò để định tuyến người dùng sau login
    /// </summary>
    public async Task<Roles?> GetRoleByIdAsync(int roleId)
    {
        return await _context.Roles.FindAsync(roleId);
    }

    /// <summary>
    /// Kiểm tra email đã tồn tại chưa (để tránh trùng lặp khi đăng ký)
    /// </summary>
    public async Task<bool> EmailExistsAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        return await _context.Users.AnyAsync(u => u.Email == email);
    }

    /// <summary>
    /// Kiểm tra username đã tồn tại chưa (để tránh trùng lặp khi đăng ký)
    /// </summary>
    public async Task<bool> UsernameExistsAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;

        return await _context.Users.AnyAsync(u => u.Username == username);
    }
}
