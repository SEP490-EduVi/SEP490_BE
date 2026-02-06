using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Authentication.Request;

public class GoogleLoginRequest
{
    [Required(ErrorMessage = "Google ID Token is required")]
    public string IdToken { get; set; } = string.Empty;
}
