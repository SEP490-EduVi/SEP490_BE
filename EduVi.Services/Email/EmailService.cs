
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace EduVi.Services.Email;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendPasswordResetEmailAsync(string email, string resetToken, string fullName)
    {
        try
        {
            var subject = "Reset Your Password - EduVi";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2 style='color: #2563eb;'>Hello {fullName},</h2>
                    <p>You requested to reset your password for your EduVi account.</p>
                    <p>Your password reset token is:</p>
                    <div style='background-color: #f3f4f6; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <strong style='font-size: 18px; color: #1f2937;'>{resetToken}</strong>
                    </div>
                    <p style='color: #ef4444;'><strong>⚠️ This token will expire in 15 minutes.</strong></p>
                    <p>If you didn't request this, please ignore this email and your password will remain unchanged.</p>
                    <br/>
                    <p style='color: #6b7280; font-size: 12px;'>
                        This is an automated message from EduVi. Please do not reply to this email.
                    </p>
                </body>
                </html>";

            return await SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", email);
            return false;
        }
    }

    public async Task<bool> SendEmailVerificationAsync(string email, string verificationToken, string fullName)
    {
        try
        {
            var subject = "Verify Your Email - EduVi";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2 style='color: #2563eb;'>Welcome to EduVi, {fullName}!</h2>
                    <p>Thank you for registering. Please verify your email address to activate your account.</p>
                    <p>Your verification token is:</p>
                    <div style='background-color: #f3f4f6; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <strong style='font-size: 18px; color: #1f2937;'>{verificationToken}</strong>
                    </div>
                    <p style='color: #ef4444;'><strong>⚠️ This token will expire in 24 hours.</strong></p>
                    <br/>
                    <p style='color: #6b7280; font-size: 12px;'>
                        This is an automated message from EduVi. Please do not reply to this email.
                    </p>
                </body>
                </html>";

            return await SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification email to {Email}", email);
            return false;
        }
    }

    public async Task<bool> SendWelcomeEmailAsync(string email, string fullName)
    {
        try
        {
            var subject = "Welcome to EduVi!";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2 style='color: #2563eb;'>Welcome to EduVi, {fullName}!</h2>
                    <p>Your account has been successfully created.</p>
                    <p>Start exploring our educational platform and enhance your learning experience!</p>
                    <br/>
                    <p>If you have any questions, feel free to contact our support team.</p>
                    <br/>
                    <p style='color: #6b7280; font-size: 12px;'>
                        This is an automated message from EduVi. Please do not reply to this email.
                    </p>
                </body>
                </html>";

            return await SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome email to {Email}", email);
            return false;
        }
    }

    private async Task<bool> SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            var smtpHost = _configuration["EmailSettings:SmtpHost"];
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderPassword = _configuration["EmailSettings:SenderPassword"];
            var senderName = _configuration["EmailSettings:SenderName"] ?? "EduVi Platform";

            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(senderEmail))
            {
                _logger.LogWarning("Email settings not configured. Skipping email send.");
                return false;
            }

            using var smtpClient = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(senderEmail, senderPassword)
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(to);

            await smtpClient.SendMailAsync(mailMessage);
            
            _logger.LogInformation("Email sent successfully to {Email}", to);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", to);
            return false;
        }
    }
}
