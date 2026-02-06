namespace EduVi.Contracts.DTOs.Authentication.Response;

public class RegisterResponse
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public int OtpExpiresIn { get; set; } = 300; // 5 minutes in seconds
}
