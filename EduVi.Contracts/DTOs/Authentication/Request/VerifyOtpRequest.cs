using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Authentication.Request;

public class VerifyOtpRequest
{
    [Required(ErrorMessage = "UserId is required")]
    public int UserId { get; set; }

    [Required(ErrorMessage = "OTP is required")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be 6 digits")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must be numeric")]
    public string Otp { get; set; } = string.Empty;
}
