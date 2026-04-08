
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
            var subject = "Đặt lại mật khẩu — EduVi";
            var body = BuildEmailLayout(
                title: "Đặt lại mật khẩu",
                preheader: "Yêu cầu đặt lại mật khẩu tài khoản EduVi của bạn.",
                content: $"""
                    <p style="margin:0 0 16px;font-size:15px;color:#0f172a;">Xin chào <strong>{fullName}</strong>,</p>
                    <p style="margin:0 0 20px;font-size:14px;color:#475569;">
                        Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản EduVi của bạn.
                        Nhập mã bên dưới để tiếp tục.
                    </p>
                    <div style="background:#f8fafc;border:1px solid #e2e8f0;border-radius:6px;padding:24px;text-align:center;margin:0 0 20px;">
                        <p style="margin:0 0 4px;font-size:11px;color:#94a3b8;text-transform:uppercase;letter-spacing:1px;font-weight:600;">Mã xác nhận</p>
                        <p style="margin:0;font-size:32px;font-weight:700;letter-spacing:8px;color:#0f172a;font-family:monospace;">{resetToken}</p>
                    </div>
                    <p style="margin:0 0 16px;font-size:13px;color:#ef4444;">Mã có hiệu lực trong <strong>15 phút</strong>. Không chia sẻ mã này với bất kỳ ai.</p>
                    <p style="margin:0;font-size:13px;color:#94a3b8;">
                        Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này — tài khoản của bạn vẫn an toàn.
                    </p>
                """);

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
            var subject = "Xác thực email — EduVi";
            var body = BuildEmailLayout(
                title: "Xác thực địa chỉ email",
                preheader: "Nhập mã OTP để kích hoạt tài khoản EduVi của bạn.",
                content: $"""
                    <p style="margin:0 0 16px;font-size:15px;color:#0f172a;">Xin chào <strong>{fullName}</strong>,</p>
                    <p style="margin:0 0 20px;font-size:14px;color:#475569;">
                        Cảm ơn bạn đã đăng ký tài khoản EduVi! Vui lòng nhập mã OTP bên dưới
                        để xác thực địa chỉ email và kích hoạt tài khoản.
                    </p>
                    <div style="background:#f8fafc;border:1px solid #e2e8f0;border-radius:6px;padding:24px;text-align:center;margin:0 0 20px;">
                        <p style="margin:0 0 4px;font-size:11px;color:#94a3b8;text-transform:uppercase;letter-spacing:1px;font-weight:600;">Mã OTP của bạn</p>
                        <p style="margin:0;font-size:36px;font-weight:700;letter-spacing:10px;color:#0f172a;font-family:monospace;">{verificationToken}</p>
                    </div>
                    <p style="margin:0 0 16px;font-size:13px;color:#ef4444;">Mã hết hạn sau <strong>5 phút</strong>. Không chia sẻ mã này với bất kỳ ai.</p>
                    <p style="margin:0;font-size:13px;color:#94a3b8;">
                        Nếu bạn không thực hiện đăng ký, vui lòng bỏ qua email này.
                    </p>
                """);

            return await SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification email to {Email}", email);
            return false;
        }
    }

    public async Task<bool> SendWelcomeEmailAsync(string email, string fullName, string roleName)
    {
        try
        {
            var (roleLabel, roleDesc, roleFeatures) = roleName.ToLower() switch
            {
                "expert" or "chuyên gia" => (
                    "Chuyên gia",
                    "Bạn có thể đăng tải học liệu, chia sẻ kiến thức và nhận doanh thu từ các giáo viên trên nền tảng.",
                    new[]
                    {
                        "Upload học liệu (hình ảnh, video)",
                        "Nhận 70% doanh thu từ mỗi lượt mua",
                        "Theo dõi thống kê bán hàng",
                        "Hoàn tất xác thực hồ sơ để bắt đầu"
                    }),
                "teacher" or "giáo viên" => (
                    "Giáo viên",
                    "Bạn có thể khám phá kho học liệu phong phú, mua tài liệu chất lượng và sử dụng các công cụ AI hỗ trợ giảng dạy.",
                    new[]
                    {
                        "Khám phá kho học liệu đa dạng",
                        "Sử dụng công cụ AI tạo bài giảng",
                        "Tạo trò chơi giáo dục tương tác",
                        "Nạp ví để mua học liệu premium"
                    }),
                _ => (
                    roleName,
                    "Chào mừng bạn đến với EduVi — nền tảng giáo dục hàng đầu.",
                    new[] { "Khám phá nền tảng EduVi" })
            };

            var featuresHtml = string.Join("\n", roleFeatures.Select(f =>
                $"""<li style="margin:8px 0;font-size:14px;color:#374151;">{f}</li>"""));

            var subject = $"Chào mừng đến với EduVi, {fullName}!";
            var body = BuildEmailLayout(
                title: "Tài khoản đã được kích hoạt",
                preheader: $"Chào mừng {fullName} — tài khoản {roleLabel} EduVi của bạn đã sẵn sàng.",
                content: $"""
                    <p style="margin:0 0 16px;font-size:16px;color:#0f172a;">Xin chào <strong>{fullName}</strong>,</p>
                    <p style="margin:0 0 20px;font-size:15px;color:#475569;">
                        Tài khoản <strong>{roleLabel}</strong> của bạn đã được xác thực thành công. Dưới đây là những gì bạn có thể làm trên EduVi.
                    </p>
                    <p style="margin:0 0 8px;font-size:13px;font-weight:600;color:#64748b;text-transform:uppercase;letter-spacing:0.5px;">Tính năng dành cho bạn</p>
                    <p style="margin:0 0 12px;font-size:14px;color:#475569;">{roleDesc}</p>
                    <ul style="margin:0 0 24px;padding-left:20px;">
                        {featuresHtml}
                    </ul>
                    <div style="background:#f8fafc;border-left:3px solid #3b82f6;border-radius:0 4px 4px 0;padding:12px 16px;margin:0 0 20px;">
                        <p style="margin:0;font-size:14px;color:#334155;">
                            <strong>Bước tiếp theo:</strong> Đăng nhập vào EduVi và hoàn thiện hồ sơ để trải nghiệm đầy đủ tính năng.
                        </p>
                    </div>
                    <p style="margin:0;font-size:13px;color:#94a3b8;">
                        Nếu bạn có câu hỏi, đội ngũ hỗ trợ EduVi luôn sẵn sàng giúp đỡ.
                    </p>
                """);

            return await SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome email to {Email}", email);
            return false;
        }
    }

    public async Task<bool> SendWithdrawalOtpEmailAsync(string email, string fullName, string otp, decimal amount)
    {
        try
        {
            var subject = "Xác nhận yêu cầu rút tiền — EduVi";
            var body = BuildEmailLayout(
                title: "Xác nhận rút tiền",
                preheader: "Mã OTP xác nhận yêu cầu rút tiền từ ví EduVi của bạn.",
                content: $"""
                    <p style="margin:0 0 16px;font-size:15px;color:#0f172a;">Xin chào <strong>{fullName}</strong>,</p>
                    <p style="margin:0 0 20px;font-size:14px;color:#475569;">
                        Chúng tôi nhận được yêu cầu rút <strong>{amount:N0} VND</strong> từ ví EduVi của bạn.
                        Nhập mã OTP bên dưới để xác nhận giao dịch.
                    </p>
                    <div style="background:#f8fafc;border:1px solid #e2e8f0;border-radius:6px;padding:24px;text-align:center;margin:0 0 20px;">
                        <p style="margin:0 0 4px;font-size:11px;color:#94a3b8;text-transform:uppercase;letter-spacing:1px;font-weight:600;">Mã OTP xác nhận rút tiền</p>
                        <p style="margin:0;font-size:36px;font-weight:700;letter-spacing:10px;color:#0f172a;font-family:monospace;">{otp}</p>
                    </div>
                    <p style="margin:0 0 16px;font-size:13px;color:#ef4444;">Mã hết hạn sau <strong>5 phút</strong>. Không chia sẻ mã này với bất kỳ ai.</p>
                    <p style="margin:0;font-size:13px;color:#94a3b8;">
                        Nếu bạn không thực hiện yêu cầu này, hãy bỏ qua email này và liên hệ hỗ trợ ngay.
                    </p>
                """);

            return await SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send withdrawal OTP email to {Email}", email);
            return false;
        }
    }

    // ── Shared layout ────────────────────────────────────────────────────────────

    private static string BuildEmailLayout(string title, string preheader, string content)
    {
        return $"""
            <!DOCTYPE html>
            <html lang="vi">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width,initial-scale=1.0">
                <title>{title}</title>
            </head>
            <body style="margin:0;padding:0;background-color:#f6f8fa;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;">
                <span style="display:none;max-height:0;overflow:hidden;">{preheader}</span>

                <table width="100%" cellpadding="0" cellspacing="0" style="background-color:#f6f8fa;padding:40px 16px;">
                  <tr>
                    <td align="center">
                      <table width="100%" style="max-width:520px;" cellpadding="0" cellspacing="0">

                        <!-- Header -->
                        <tr>
                          <td align="center" style="padding-bottom:20px;">
                            <span style="font-size:20px;font-weight:700;color:#0f172a;letter-spacing:-0.3px;">EduVi</span>
                          </td>
                        </tr>

                        <!-- Card -->
                        <tr>
                          <td style="background:#ffffff;border-radius:8px;padding:32px 40px;border:1px solid #e2e8f0;">
                            <h1 style="margin:0 0 20px;font-size:18px;font-weight:600;color:#0f172a;">{title}</h1>
                            <hr style="margin:0 0 24px;border:none;border-top:1px solid #e2e8f0;">
                            {content}
                          </td>
                        </tr>

                        <!-- Footer -->
                        <tr>
                          <td align="center" style="padding-top:20px;">
                            <p style="margin:0 0 4px;font-size:12px;color:#94a3b8;">© 2026 EduVi Platform. All rights reserved.</p>
                            <p style="margin:0;font-size:12px;color:#cbd5e1;">Email này được gửi tự động, vui lòng không trả lời.</p>
                          </td>
                        </tr>

                      </table>
                    </td>
                  </tr>
                </table>
            </body>
            </html>
            """;
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
