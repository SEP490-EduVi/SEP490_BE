using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Authentication.Request;

public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;
}
