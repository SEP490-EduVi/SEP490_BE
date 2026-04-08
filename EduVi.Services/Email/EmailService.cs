
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
                    <p style="margin:0 0 16px;font-size:16px;color:#374151;">Xin chào <strong>{fullName}</strong>,</p>
                    <p style="margin:0 0 16px;font-size:15px;color:#6b7280;">
                        Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản EduVi của bạn.
                        Nhập mã bên dưới để tiếp tục.
                    </p>
                    <div style="background:#f0f9ff;border:2px dashed #0ea5e9;border-radius:12px;padding:24px;text-align:center;margin:24px 0;">
                        <p style="margin:0 0 8px;font-size:13px;color:#0369a1;text-transform:uppercase;letter-spacing:1px;font-weight:600;">Mã xác nhận</p>
                        <p style="margin:0;font-size:36px;font-weight:700;letter-spacing:8px;color:#0c4a6e;font-family:monospace;">{resetToken}</p>
                    </div>
                    <div style="background:#fef2f2;border-left:4px solid #ef4444;border-radius:0 8px 8px 0;padding:12px 16px;margin:0 0 20px;">
                        <p style="margin:0;font-size:14px;color:#b91c1c;">
                            ⏱ Mã có hiệu lực trong <strong>15 phút</strong>. Không chia sẻ mã này với bất kỳ ai.
                        </p>
                    </div>
                    <p style="margin:0;font-size:14px;color:#9ca3af;">
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
                    <p style="margin:0 0 16px;font-size:16px;color:#374151;">Xin chào <strong>{fullName}</strong>,</p>
                    <p style="margin:0 0 16px;font-size:15px;color:#6b7280;">
                        Cảm ơn bạn đã đăng ký tài khoản EduVi! Vui lòng nhập mã OTP bên dưới
                        để xác thực địa chỉ email và kích hoạt tài khoản.
                    </p>
                    <div style="background:linear-gradient(135deg,#eff6ff,#dbeafe);border:2px solid #3b82f6;border-radius:16px;padding:28px;text-align:center;margin:24px 0;">
                        <p style="margin:0 0 8px;font-size:13px;color:#1d4ed8;text-transform:uppercase;letter-spacing:1px;font-weight:600;">Mã OTP của bạn</p>
                        <p style="margin:0;font-size:42px;font-weight:700;letter-spacing:10px;color:#1e3a8a;font-family:monospace;">{verificationToken}</p>
                    </div>
                    <div style="background:#fef2f2;border-left:4px solid #ef4444;border-radius:0 8px 8px 0;padding:12px 16px;margin:0 0 20px;">
                        <p style="margin:0;font-size:14px;color:#b91c1c;">
                            ⏱ Mã hết hạn sau <strong>5 phút</strong>. Không chia sẻ mã này với bất kỳ ai.
                        </p>
                    </div>
                    <p style="margin:0;font-size:14px;color:#9ca3af;">
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
            var (roleLabel, roleIcon, roleDesc, roleFeatures) = roleName.ToLower() switch
            {
                "expert" or "chuyên gia" => (
                    "Chuyên gia",
                    "🎓",
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
                    "👩‍🏫",
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
                    "👤",
                    "Chào mừng bạn đến với EduVi — nền tảng giáo dục hàng đầu.",
                    new[] { "Khám phá nền tảng EduVi" })
            };

            var featuresHtml = string.Join("\n", roleFeatures.Select(f =>
                $"""<li style="margin:8px 0;font-size:14px;color:#374151;">{f}</li>"""));

            var subject = $"Chào mừng đến với EduVi, {fullName}!";
            var body = BuildEmailLayout(
                title: $"{roleIcon} Tài khoản đã được kích hoạt!",
                preheader: $"Chào mừng {fullName} — tài khoản {roleLabel} EduVi của bạn đã sẵn sàng.",
                content: $"""
                    <div style="text-align:center;margin-bottom:24px;">
                        <div style="display:inline-block;background:linear-gradient(135deg,#2563eb,#7c3aed);border-radius:50%;width:72px;height:72px;line-height:72px;font-size:36px;">{roleIcon}</div>
                    </div>
                    <p style="margin:0 0 8px;font-size:20px;font-weight:700;color:#111827;text-align:center;">Chào mừng, {fullName}!</p>
                    <p style="margin:0 0 24px;font-size:14px;color:#6b7280;text-align:center;">Tài khoản <strong>{roleLabel}</strong> của bạn đã được xác thực thành công.</p>

                    <div style="background:#f8fafc;border-radius:12px;padding:20px 24px;margin:0 0 24px;">
                        <p style="margin:0 0 12px;font-size:14px;font-weight:600;color:#1e40af;text-transform:uppercase;letter-spacing:0.5px;">Bạn có thể làm gì?</p>
                        <p style="margin:0 0 12px;font-size:14px;color:#6b7280;">{roleDesc}</p>
                        <ul style="margin:0;padding-left:20px;">
                            {featuresHtml}
                        </ul>
                    </div>

                    <div style="background:linear-gradient(135deg,#eff6ff,#f5f3ff);border-radius:12px;padding:16px 20px;margin:0 0 20px;border:1px solid #e0e7ff;">
                        <p style="margin:0;font-size:14px;color:#4338ca;">
                                <strong>Bước tiếp theo:</strong> Đăng nhập vào EduVi và hoàn thiện hồ sơ của bạn để trải nghiệm đầy đủ tính năng.
                        </p>
                    </div>

                    <p style="margin:0;font-size:13px;color:#9ca3af;text-align:center;">
                        Nếu bạn có bất kỳ câu hỏi nào, đội ngũ hỗ trợ EduVi luôn sẵn sàng giúp đỡ.
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
                    <p style="margin:0 0 16px;font-size:16px;color:#374151;">Xin chào <strong>{fullName}</strong>,</p>
                    <p style="margin:0 0 16px;font-size:15px;color:#6b7280;">
                        Chúng tôi nhận được yêu cầu rút <strong>{amount:N0} VND</strong> từ ví EduVi của bạn.
                        Nhập mã OTP bên dưới để xác nhận giao dịch.
                    </p>
                    <div style="background:linear-gradient(135deg,#f0fdf4,#dcfce7);border:2px solid #22c55e;border-radius:16px;padding:28px;text-align:center;margin:24px 0;">
                        <p style="margin:0 0 8px;font-size:13px;color:#15803d;text-transform:uppercase;letter-spacing:1px;font-weight:600;">Mã OTP xác nhận rút tiền</p>
                        <p style="margin:0;font-size:42px;font-weight:700;letter-spacing:10px;color:#14532d;font-family:monospace;">{otp}</p>
                    </div>
                    <div style="background:#fef2f2;border-left:4px solid #ef4444;border-radius:0 8px 8px 0;padding:12px 16px;margin:0 0 20px;">
                        <p style="margin:0;font-size:14px;color:#b91c1c;">
                            ⏱ Mã hết hạn sau <strong>5 phút</strong>. Không chia sẻ mã này với bất kỳ ai.
                        </p>
                    </div>
                    <p style="margin:0;font-size:14px;color:#9ca3af;">
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
            <body style="margin:0;padding:0;background-color:#f1f5f9;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;">
                <!-- Preheader (hidden preview text) -->
                <span style="display:none;max-height:0;overflow:hidden;">{preheader}</span>

                <table width="100%" cellpadding="0" cellspacing="0" style="background-color:#f1f5f9;padding:40px 16px;">
                  <tr>
                    <td align="center">
                      <table width="100%" style="max-width:560px;" cellpadding="0" cellspacing="0">

                        <!-- Logo/Header -->
                        <tr>
                          <td align="center" style="padding-bottom:24px;">
                            <div style="display:inline-flex;align-items:center;gap:8px;">
                              <div style="background:linear-gradient(135deg,#2563eb,#7c3aed);border-radius:10px;width:40px;height:40px;line-height:40px;text-align:center;font-size:22px;">🎓</div>
                              <span style="font-size:24px;font-weight:800;color:#1e3a8a;letter-spacing:-0.5px;">EduVi</span>
                            </div>
                          </td>
                        </tr>

                        <!-- Card -->
                        <tr>
                          <td style="background:#ffffff;border-radius:20px;padding:36px 40px;box-shadow:0 4px 24px rgba(0,0,0,0.06);">
                            <!-- Title -->
                            <h1 style="margin:0 0 28px;font-size:22px;font-weight:700;color:#111827;border-bottom:2px solid #f0f4ff;padding-bottom:20px;">{title}</h1>
                            <!-- Dynamic content -->
                            {content}
                          </td>
                        </tr>

                        <!-- Footer -->
                        <tr>
                          <td align="center" style="padding-top:24px;">
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
