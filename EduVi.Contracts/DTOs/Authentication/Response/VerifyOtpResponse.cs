namespace EduVi.Contracts.DTOs.Authentication.Response;

public class VerifyOtpResponse
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
}
