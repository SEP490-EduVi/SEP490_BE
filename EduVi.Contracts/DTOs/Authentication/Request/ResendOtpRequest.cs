using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Authentication.Request;

public class ResendOtpRequest
{
    [Required(ErrorMessage = "UserId is required")]
    public int UserId { get; set; }
}
