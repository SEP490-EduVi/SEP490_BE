namespace EduVi.Contracts.DTOs.Authentication.Response;

public class ResendOtpResponse
{
    public DateTimeOffset CanResendAgainAt { get; set; }
}
