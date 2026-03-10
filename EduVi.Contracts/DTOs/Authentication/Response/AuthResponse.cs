namespace EduVi.Contracts.DTOs.Authentication.Response;

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; } // Thời gian hết hạn (giây)
    public UserInfo User { get; set; } = null!;
}

public class UserInfo
{
    public int UserId { get; set; }
    public string UserCode { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public int Status { get; set; }
    public RoleInfo Role { get; set; } = null!;
    
    // Thông tin bổ sung theo vai trò
    public int? AdminId { get; set; }
    public int? ExpertId { get; set; }
    public int? StaffId { get; set; }
    public int? TeacherId { get; set; }

    /// <summary>
    /// Trạng thái xác thực của Expert.
    /// null = chưa nộp hồ sơ hoặc đang chờ duyệt
    /// true = đã được Staff duyệt → full Expert access
    /// false = bị từ chối → cần nộp lại
    /// Chỉ có giá trị khi Role = Expert, các role khác luôn null.
    /// </summary>
    public bool? ExpertIsVerified { get; set; }
}

public class RoleInfo
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
}
