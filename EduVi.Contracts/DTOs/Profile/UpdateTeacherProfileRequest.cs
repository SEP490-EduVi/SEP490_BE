using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Profile;

public class UpdateTeacherProfileRequest
{
    [StringLength(100, ErrorMessage = "FullName cannot exceed 100 characters")]
    public string? FullName { get; set; }

    [Phone(ErrorMessage = "Invalid phone number format")]
    public string? PhoneNumber { get; set; }

    [StringLength(500, ErrorMessage = "AvatarUrl cannot exceed 500 characters")]
    public string? AvatarUrl { get; set; }

    public string? SchoolName { get; set; }
}
