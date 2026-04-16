using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Admin.Request;

/// <summary>
/// Admin tạo tài khoản người dùng mới.
/// </summary>
public class CreateUserRequest
{
    [Required(ErrorMessage = "Username is required")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
        ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "FullName is required")]
    [StringLength(100, ErrorMessage = "FullName cannot exceed 100 characters")]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Invalid phone number format")]
    public string? PhoneNumber { get; set; }

    [StringLength(500, ErrorMessage = "AvatarUrl cannot exceed 500 characters")]
    public string? AvatarUrl { get; set; }

    [Required(ErrorMessage = "RoleId is required")]
    public int RoleId { get; set; }
}
