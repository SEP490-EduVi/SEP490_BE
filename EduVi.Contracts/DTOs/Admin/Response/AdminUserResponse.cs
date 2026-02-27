namespace EduVi.Contracts.DTOs.Admin.Response;

/// <summary>
/// Thông tin chi tiết user cho Admin (bao gồm role, trạng thái, sub-entity IDs)
/// </summary>
public class AdminUserResponse
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// 0 = Banned, 1 = Active
    /// </summary>
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;

    public bool IsEmailVerified { get; set; }
    public DateTime? CreatedAt { get; set; }

    // Role info
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;

    // Sub-entity IDs (null nếu không phải role đó)
    public int? AdminId { get; set; }
    public int? TeacherId { get; set; }
    public int? ExpertId { get; set; }
    public int? StaffId { get; set; }
}
