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

        // Generate UserCode nếu chưa có
        if (string.IsNullOrEmpty(user.UserCode))
        {
            user.UserCode = await GenerateUniqueCodeAsync("USER");
        }

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

    /// <summary>
    /// Tạo Admin record với auto-generated AdminCode
    /// </summary>
    public async Task<Admins> CreateAdminAsync(int userId)
    {
        var adminCode = await GenerateUniqueCodeAsync("ADMIN");
        
        var admin = new Admins
        {
            AdminId = userId,
            AdminCode = adminCode
        };

        _context.Admins.Add(admin);
        await _context.SaveChangesAsync();

        return admin;
    }

    /// <summary>
    /// Tạo Expert record với auto-generated ExpertCode
    /// </summary>
    public async Task<Experts> CreateExpertAsync(int userId)
    {
        var expertCode = await GenerateUniqueCodeAsync("EXPERT");
        
        var expert = new Experts
        {
            ExpertId = userId,
            ExpertCode = expertCode,
            IsVerified = false // Mặc định chưa verify
        };

        _context.Experts.Add(expert);
        await _context.SaveChangesAsync();

        return expert;
    }

    /// <summary>
    /// Tạo Teacher record với auto-generated TeacherCode
    /// </summary>
    public async Task<Teachers> CreateTeacherAsync(int userId)
    {
        var teacherCode = await GenerateUniqueCodeAsync("TEACHER");
        
        var teacher = new Teachers
        {
            TeacherId = userId,
            TeacherCode = teacherCode
        };

        _context.Teachers.Add(teacher);
        await _context.SaveChangesAsync();

        return teacher;
    }

    /// <summary>
    /// Tạo Staff record với auto-generated StaffCode
    /// </summary>
    public async Task<Staffs> CreateStaffAsync(int userId)
    {
        var staffCode = await GenerateUniqueCodeAsync("STAFF");
        
        var staff = new Staffs
        {
            StaffId = userId,
            StaffCode = staffCode
        };

        _context.Staffs.Add(staff);
        await _context.SaveChangesAsync();

        return staff;
    }

    /// <summary>
    /// Generate unique code for role-specific table
    /// Format: PREFIX + 6-digit number (e.g., ADMIN000001, TEACHER000001)
    /// </summary>
    public async Task<string> GenerateUniqueCodeAsync(string prefix)
    {
        int maxAttempts = 100;
        int attempt = 0;

        while (attempt < maxAttempts)
        {
            // Lấy số thứ tự cao nhất hiện tại cho prefix này
            int maxNumber = 0;

            switch (prefix)
            {
                case "USER":
                    var lastUser = await _context.Users
                        .Where(u => u.UserCode.StartsWith(prefix))
                        .OrderByDescending(u => u.UserCode)
                        .FirstOrDefaultAsync();
                    if (lastUser != null && lastUser.UserCode.Length > prefix.Length)
                    {
                        int.TryParse(lastUser.UserCode.Substring(prefix.Length), out maxNumber);
                    }
                    break;

                case "ADMIN":
                    var lastAdmin = await _context.Admins
                        .Where(a => a.AdminCode.StartsWith(prefix))
                        .OrderByDescending(a => a.AdminCode)
                        .FirstOrDefaultAsync();
                    if (lastAdmin != null && lastAdmin.AdminCode.Length > prefix.Length)
                    {
                        int.TryParse(lastAdmin.AdminCode.Substring(prefix.Length), out maxNumber);
                    }
                    break;

                case "EXPERT":
                    var lastExpert = await _context.Experts
                        .Where(e => e.ExpertCode.StartsWith(prefix))
                        .OrderByDescending(e => e.ExpertCode)
                        .FirstOrDefaultAsync();
                    if (lastExpert != null && lastExpert.ExpertCode.Length > prefix.Length)
                    {
                        int.TryParse(lastExpert.ExpertCode.Substring(prefix.Length), out maxNumber);
                    }
                    break;

                case "TEACHER":
                    var lastTeacher = await _context.Teachers
                        .Where(t => t.TeacherCode.StartsWith(prefix))
                        .OrderByDescending(t => t.TeacherCode)
                        .FirstOrDefaultAsync();
                    if (lastTeacher != null && lastTeacher.TeacherCode.Length > prefix.Length)
                    {
                        int.TryParse(lastTeacher.TeacherCode.Substring(prefix.Length), out maxNumber);
                    }
                    break;

                case "STAFF":
                    var lastStaff = await _context.Staffs
                        .Where(s => s.StaffCode.StartsWith(prefix))
                        .OrderByDescending(s => s.StaffCode)
                        .FirstOrDefaultAsync();
                    if (lastStaff != null && lastStaff.StaffCode.Length > prefix.Length)
                    {
                        int.TryParse(lastStaff.StaffCode.Substring(prefix.Length), out maxNumber);
                    }
                    break;
            }

            // Tăng số lên 1 và format với 6 chữ số
            string code = $"{prefix}{(maxNumber + 1).ToString("D6")}";

            // Kiểm tra xem code đã tồn tại chưa (để tránh race condition)
            bool exists = false;
            switch (prefix)
            {
                case "USER":
                    exists = await _context.Users.AnyAsync(u => u.UserCode == code);
                    break;
                case "ADMIN":
                    exists = await _context.Admins.AnyAsync(a => a.AdminCode == code);
                    break;
                case "EXPERT":
                    exists = await _context.Experts.AnyAsync(e => e.ExpertCode == code);
                    break;
                case "TEACHER":
                    exists = await _context.Teachers.AnyAsync(t => t.TeacherCode == code);
                    break;
                case "STAFF":
                    exists = await _context.Staffs.AnyAsync(s => s.StaffCode == code);
                    break;
            }

            if (!exists)
            {
                return code;
            }

            attempt++;
        }

        throw new InvalidOperationException($"Failed to generate unique code for {prefix} after {maxAttempts} attempts");
    }
}
