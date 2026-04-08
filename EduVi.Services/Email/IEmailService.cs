namespace EduVi.Services.Email;

public interface IEmailService
{
    /// <summary>
    /// Send password reset email with token
    /// </summary>
    Task<bool> SendPasswordResetEmailAsync(string email, string resetToken, string fullName);
    
    /// <summary>
    /// Send email verification for new registration
    /// </summary>
    Task<bool> SendEmailVerificationAsync(string email, string verificationToken, string fullName);
    
    /// <summary>
    /// Send welcome email after successful email verification
    /// </summary>
    Task<bool> SendWelcomeEmailAsync(string email, string fullName, string roleName);

    /// <summary>
    /// Send OTP email for withdrawal confirmation
    /// </summary>
    Task<bool> SendWithdrawalOtpEmailAsync(string email, string fullName, string otp, decimal amount);
}
