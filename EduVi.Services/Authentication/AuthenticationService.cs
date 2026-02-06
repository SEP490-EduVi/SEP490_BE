using EduVi.Contracts.DTOs.Authentication.Request;
using EduVi.Contracts.DTOs.Authentication.Response;
using EduVi.Contracts.Repositories;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using EduVi.Services.Email;
using EduVi.Services.RateLimit;
using EduVi.Services.Otp;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace EduVi.Services.Authentication;

public class AuthenticationService : IAuthenticationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _redisDb;
    private readonly IRateLimitService _rateLimitService;
    private readonly IEmailService _emailService;
    private readonly IOtpService _otpService;

    public AuthenticationService(
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        IConnectionMultiplexer redis,
        IRateLimitService rateLimitService,
        IEmailService emailService,
        IOtpService otpService)
    {
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _redis = redis;
        _redisDb = redis.GetDatabase();
        _rateLimitService = rateLimitService;
        _emailService = emailService;
        _otpService = otpService;
    }

    #region Login & Register

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        // 0. Rate Limiting - 5 attempts per 5 minutes per username
        var isAllowed = await _rateLimitService.IsAllowedAsync($"login:{request.Username}", 5, 5);
        if (!isAllowed)
        {
            var remaining = await _rateLimitService.GetRemainingAttemptsAsync($"login:{request.Username}", 5, 5);
            throw new UnauthorizedAccessException($"Too many login attempts. Please try again in 5 minutes. Remaining attempts: {remaining}");
        }

        // 1. Tìm người dùng
        var user = await _unitOfWork.AuthenticationRepository.GetUserByUsernameAsync(request.Username);
        if (user == null)
            throw new UnauthorizedAccessException("Invalid username or password");

        // 2. Kiểm tra mật khẩu
        if (!VerifyPassword(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid username or password");

        // 3. Kiểm tra trạng thái tài khoản
        if (user.Status == 0) // Banned
            throw new UnauthorizedAccessException("Your account has been banned");

        // 4. CRITICAL: Check email verification status
        if (!user.IsEmailVerified)
            throw new UnauthorizedAccessException("Please verify your email before logging in. Check your inbox for OTP.");

        // 5. Login thành công - reset rate limit
        await _rateLimitService.ResetAsync($"login:{request.Username}");

        // 6. Tạo JWT Token
        var token = GenerateJwtToken(user);

        // 6. Lưu Token vào Redis
        var expiresIn = int.Parse(_configuration["Jwt:ExpiresInMinutes"] ?? "60");
        await SaveTokenToRedisAsync(user.UserId, token, TimeSpan.FromMinutes(expiresIn));

        // 7. Trả về response
        return new AuthResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresIn = expiresIn * 60, // Đổi sang giây
            User = MapToUserInfo(user)
        };
    }

    public async Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest request)
    {
        try
        {
            // 1. Xác thực Google ID Token
            var payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _configuration["Google:ClientId"] ?? "" }
            });

            // 2. Tìm hoặc tạo người dùng
            var user = await _unitOfWork.AuthenticationRepository.GetUserByEmailAsync(payload.Email);

            if (user == null)
            {
                // Tạo người dùng mới từ Google
                user = new Users
                {
                    Email = payload.Email,
                    Username = payload.Email.Split('@')[0] + "_" + Guid.NewGuid().ToString().Substring(0, 8),
                    FullName = payload.Name,
                    AvatarUrl = payload.Picture,
                    PasswordHash = HashPassword(Guid.NewGuid().ToString()), // Random password
                    RoleId = 5, // Default: Teacher role (customize theo nhu cầu)
                    Status = 1, // Active
                    CreatedAt = DateTime.UtcNow
                };

                user = await _unitOfWork.AuthenticationRepository.CreateUserAsync(user);
            }

            // 3. Kiểm tra trạng thái
            if (user.Status == 0)
                throw new UnauthorizedAccessException("Your account has been banned");

            // 4. Tạo JWT Token
            var token = GenerateJwtToken(user);

            // 5. Lưu Token vào Redis
            var expiresIn = int.Parse(_configuration["Jwt:ExpiresInMinutes"] ?? "60");
            await SaveTokenToRedisAsync(user.UserId, token, TimeSpan.FromMinutes(expiresIn));

            // 6. Trả về response
            return new AuthResponse
            {
                AccessToken = token,
                TokenType = "Bearer",
                ExpiresIn = expiresIn * 60,
                User = MapToUserInfo(user)
            };
        }
        catch (InvalidJwtException)
        {
            throw new UnauthorizedAccessException("Invalid Google token");
        }
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        // 1. Kiểm tra email đã tồn tại
        if (await _unitOfWork.AuthenticationRepository.EmailExistsAsync(request.Email))
            throw new InvalidOperationException("Email already exists");

        // 2. Kiểm tra username đã tồn tại
        if (await _unitOfWork.AuthenticationRepository.UsernameExistsAsync(request.Username))
            throw new InvalidOperationException("Username already exists");

        // 3. Tạo người dùng mới với trạng thái PENDING và CHƯA VERIFY
        var user = new Users
        {
            Username = request.Username,
            Email = request.Email,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            AvatarUrl = request.AvatarUrl,
            PasswordHash = HashPassword(request.Password),
            RoleId = request.RoleId,
            Status = 0, // Pending - chỉ Active sau khi verify OTP
            IsEmailVerified = false, // QUAN TRỌNG: Chưa verify
            CreatedAt = DateTime.UtcNow
        };

        user = await _unitOfWork.AuthenticationRepository.CreateUserAsync(user);

        // 4. Generate OTP (6 digits)
        var otp = _otpService.GenerateOtp();
        
        // 5. Save OTP to Redis (5 minutes TTL)
        await _otpService.SaveOtpAsync(user.UserId, otp, ttlMinutes: 5);

        // 6. Send OTP email (async, non-blocking)
        try
        {
            await SendOtpEmailAsync(user.Email, otp, user.FullName);
            Console.WriteLine($"[OTP] ✓ OTP email sent to {user.Email}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OTP] ✗ Failed to send OTP email: {ex.Message}");
            // Continue - user can request resend
        }

        // 7. KHÔNG trả về JWT token - user phải verify OTP trước
        return new RegisterResponse
        {
            UserId = user.UserId,
            Email = user.Email,
            OtpExpiresIn = 300 // 5 minutes in seconds
        };
    }

    public async Task<VerifyOtpResponse> VerifyOtpAsync(VerifyOtpRequest request)
    {
        // 1. Check brute-force protection
        var failedAttempts = await _otpService.GetFailedAttemptsAsync(request.UserId);
        if (failedAttempts >= 5)
            throw new UnauthorizedAccessException("Too many failed attempts. Please request a new OTP.");

        // 2. Verify OTP from Redis
        var isValid = await _otpService.VerifyOtpAsync(request.UserId, request.Otp);
        
        if (!isValid)
        {
            // Increment failed attempts
            await _otpService.IncrementFailedAttemptsAsync(request.UserId);
            throw new UnauthorizedAccessException("Invalid or expired OTP");
        }

        // 3. Get user from DB
        var user = await _unitOfWork.AuthenticationRepository.GetUserByIdAsync(request.UserId);
        if (user == null)
            throw new InvalidOperationException("User not found");

        // 4. Update user status: Active + EmailVerified
        user.Status = 1; // Active
        user.IsEmailVerified = true;

        try
        {
            // Update user in database
            await _unitOfWork.AuthenticationRepository.UpdateUserAsync(user);
            await _unitOfWork.SaveChangesWithTransactionAsync();

            // 5. Clean up Redis keys (only after DB success)
            await _otpService.RevokeOtpAsync(request.UserId);
            await _otpService.ResetFailedAttemptsAsync(request.UserId);

            Console.WriteLine($"[OTP] ✓ Email verified for userId {request.UserId}");

            return new VerifyOtpResponse
            {
                UserId = user.UserId,
                Email = user.Email,
                IsVerified = true
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OTP] ✗ Failed to update user verification status: {ex.Message}");
            throw new Exception("Failed to verify email. Please try again.", ex);
        }
    }

    public async Task<ResendOtpResponse> ResendOtpAsync(ResendOtpRequest request)
    {
        // 1. Check if user exists
        var user = await _unitOfWork.AuthenticationRepository.GetUserByIdAsync(request.UserId);
        if (user == null)
            throw new InvalidOperationException("User not found");

        // 2. Check if already verified
        if (user.IsEmailVerified)
            throw new InvalidOperationException("Email already verified");

        // 3. Check cooldown (60 seconds)
        var canResend = await _otpService.CanResendOtpAsync(request.UserId);
        if (!canResend)
            throw new InvalidOperationException("Please wait 60 seconds before requesting a new OTP");

        // 4. Check daily limit (5 times per day)
        var resendCount = await _otpService.GetResendCountAsync(request.UserId);
        if (resendCount >= 5)
            throw new InvalidOperationException("Maximum resend limit reached (5/day). Please try again tomorrow.");

        // 5. Revoke old OTP
        await _otpService.RevokeOtpAsync(request.UserId);

        // 6. Generate new OTP
        var otp = _otpService.GenerateOtp();
        
        // 7. Save new OTP
        await _otpService.SaveOtpAsync(request.UserId, otp, ttlMinutes: 5);

        // 8. Increment resend counter (with cooldown)
        await _otpService.IncrementResendCountAsync(request.UserId);

        // 9. Send email
        try
        {
            await SendOtpEmailAsync(user.Email, otp, user.FullName);
            Console.WriteLine($"[OTP] ✓ OTP resent to {user.Email} ({resendCount + 1}/5)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OTP] ✗ Failed to resend OTP email: {ex.Message}");
            throw new Exception("Failed to send OTP email", ex);
        }

        return new ResendOtpResponse
        {
            CanResendAgainAt = DateTime.UtcNow.AddSeconds(60)
        };
    }

    private async Task SendOtpEmailAsync(string email, string otp, string fullName)
    {
        var subject = "Verify Your Email - EduVi OTP";
        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <h2 style='color: #2563eb;'>Welcome to EduVi, {fullName}!</h2>
                <p>Thank you for registering. Please use the OTP below to verify your email address.</p>
                <div style='background-color: #f3f4f6; padding: 20px; border-radius: 5px; margin: 20px 0; text-align: center;'>
                    <h1 style='color: #1f2937; letter-spacing: 8px; margin: 0;'>{otp}</h1>
                </div>
                <p style='color: #ef4444;'><strong>⚠️ This OTP will expire in 5 minutes.</strong></p>
                <p>If you didn't request this, please ignore this email.</p>
                <br/>
                <p style='color: #6b7280; font-size: 12px;'>
                    This is an automated message from EduVi. Please do not reply to this email.
                </p>
            </body>
            </html>";

        // Reuse existing email service infrastructure
        var smtpHost = _configuration["EmailSettings:SmtpHost"];
        var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
        var senderEmail = _configuration["EmailSettings:SenderEmail"];
        var senderPassword = _configuration["EmailSettings:SenderPassword"];

        using var smtpClient = new System.Net.Mail.SmtpClient(smtpHost, smtpPort)
        {
            EnableSsl = true,
            Credentials = new System.Net.NetworkCredential(senderEmail, senderPassword)
        };

        using var mailMessage = new System.Net.Mail.MailMessage
        {
            From = new System.Net.Mail.MailAddress(senderEmail, "EduVi Platform"),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };

        mailMessage.To.Add(email);
        await smtpClient.SendMailAsync(mailMessage);
    }

    private async Task SendResetPasswordOtpEmailAsync(string email, string otp, string fullName)
    {
        var subject = "Reset Your Password - EduVi OTP";
        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <h2 style='color: #ef4444;'>Password Reset Request</h2>
                <p>Hello {fullName},</p>
                <p>We received a request to reset your password. Please use the OTP below to reset your password.</p>
                <div style='background-color: #fef2f2; padding: 20px; border-radius: 5px; margin: 20px 0; text-align: center; border: 2px solid #ef4444;'>
                    <h1 style='color: #991b1b; letter-spacing: 8px; margin: 0;'>{otp}</h1>
                </div>
                <p style='color: #ef4444;'><strong>⚠️ This OTP will expire in 5 minutes.</strong></p>
                <p style='color: #dc2626;'><strong>Security Warning:</strong> If you didn't request this password reset, please ignore this email or contact support if you're concerned about your account security.</p>
                <br/>
                <p style='color: #6b7280; font-size: 12px;'>
                    This is an automated message from EduVi. Please do not reply to this email.
                </p>
            </body>
            </html>";

        // Reuse existing email service infrastructure
        var smtpHost = _configuration["EmailSettings:SmtpHost"];
        var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
        var senderEmail = _configuration["EmailSettings:SenderEmail"];
        var senderPassword = _configuration["EmailSettings:SenderPassword"];

        using var smtpClient = new System.Net.Mail.SmtpClient(smtpHost, smtpPort)
        {
            EnableSsl = true,
            Credentials = new System.Net.NetworkCredential(senderEmail, senderPassword)
        };

        using var mailMessage = new System.Net.Mail.MailMessage
        {
            From = new System.Net.Mail.MailAddress(senderEmail, "EduVi Platform"),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };

        mailMessage.To.Add(email);
        await smtpClient.SendMailAsync(mailMessage);
    }

    #endregion

    #region Logout & Session Management

    public async Task<bool> LogoutAsync(int userId)
    {
        try
        {
            // Xóa token từ Redis
            var key = $"token:{userId}";
            await _redisDb.KeyDeleteAsync(key);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> VerifySessionAsync(int userId, string token)
    {
        try
        {
            var key = $"token:{userId}";
            var storedToken = await _redisDb.StringGetAsync(key);

            if (!storedToken.HasValue)
                return false;

            return storedToken.ToString() == token;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RevokeTokenAsync(int userId)
    {
        // Tương tự Logout, xóa token khỏi Redis
        return await LogoutAsync(userId);
    }

    #endregion

    #region User Info

    public async Task<UserInfo?> GetCurrentUserAsync(int userId)
    {
        var user = await _unitOfWork.AuthenticationRepository.GetUserByIdAsync(userId);
        if (user == null)
            return null;

        return MapToUserInfo(user);
    }

    #endregion

    #region Password Management

    public async Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        // 1. Tìm người dùng
        var user = await _unitOfWork.AuthenticationRepository.GetUserByEmailAsync(request.Email);
        if (user == null)
            return false; // Không tiết lộ email có tồn tại hay không

        // 2. Tạo OTP 6 chữ số
        var otp = _otpService.GenerateOtp();

        // 3. Lưu OTP vào Redis với TTL 5 phút (dùng key khác với registration OTP)
        await _otpService.SaveOtpAsync(user.UserId, otp, keyPrefix: "otp:reset:");

        // 4. Reset rate limiting counters cho password reset flow
        await _otpService.ResetFailedAttemptsAsync(user.UserId, keyPrefix: "otp:reset:attempts:");

        // 5. Gửi email với OTP
        try
        {
            await SendResetPasswordOtpEmailAsync(user.Email, otp, user.FullName);
            Console.WriteLine($"[EMAIL] ✓ Reset password OTP sent to {user.Email}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EMAIL] ✗ Failed to send email: {ex.Message}");
            // Continue execution - OTP is already saved in Redis
        }

        return true;
    }

    public async Task<ResendOtpResponse> ResendResetPasswordOtpAsync(string email)
    {
        // 1. Tìm người dùng
        var user = await _unitOfWork.AuthenticationRepository.GetUserByEmailAsync(email);
        if (user == null)
            throw new UnauthorizedAccessException("User not found");

        // 2. Check cooldown (60 giây)
        var canResend = await _otpService.CanResendOtpAsync(user.UserId, keyPrefix: "otp:reset:resend:cooldown:");
        if (!canResend)
        {
            var cooldownKey = $"otp:reset:resend:cooldown:{user.UserId}";
            var ttl = await _redisDb.KeyTimeToLiveAsync(cooldownKey);
            var canResendAt = DateTimeOffset.UtcNow.AddSeconds(ttl?.TotalSeconds ?? 60);
            return new ResendOtpResponse { CanResendAgainAt = canResendAt };
        }

        // 3. Check daily limit (5 lần/ngày)
        var resendCount = await _otpService.GetResendCountAsync(user.UserId, keyPrefix: "otp:reset:resend:limit:");
        if (resendCount >= 5)
        {
            throw new InvalidOperationException("You have reached the maximum number of resend attempts for today (5). Please try again tomorrow.");
        }

        // 4. Revoke OTP cũ
        await _otpService.RevokeOtpAsync(user.UserId, keyPrefix: "otp:reset:");

        // 5. Tạo OTP mới
        var otp = _otpService.GenerateOtp();
        await _otpService.SaveOtpAsync(user.UserId, otp, keyPrefix: "otp:reset:");

        // 6. Increment resend counter
        await _otpService.IncrementResendCountAsync(user.UserId, keyPrefix: "otp:reset:resend:limit:");

        // 7. Gửi email OTP mới
        try
        {
            await SendResetPasswordOtpEmailAsync(user.Email, otp, user.FullName);
            Console.WriteLine($"[EMAIL] ✓ Reset password OTP resent to {user.Email}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EMAIL] ✗ Failed to send email: {ex.Message}");
        }

        return new ResendOtpResponse
        {
            CanResendAgainAt = DateTimeOffset.UtcNow.AddSeconds(60) // Cooldown 60s
        };
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
    {
        // 1. Tìm người dùng
        var user = await _unitOfWork.AuthenticationRepository.GetUserByEmailAsync(request.Email);
        if (user == null)
            throw new UnauthorizedAccessException("User not found");

        // 2. Check brute-force protection (max 5 failed attempts)
        var failedAttempts = await _otpService.GetFailedAttemptsAsync(user.UserId, keyPrefix: "otp:reset:attempts:");
        if (failedAttempts >= 5)
        {
            throw new UnauthorizedAccessException("Too many failed OTP attempts. Please request a new OTP.");
        }

        // 3. Verify OTP từ Redis
        var isValid = await _otpService.VerifyOtpAsync(user.UserId, request.Otp, keyPrefix: "otp:reset:");
        if (!isValid)
        {
            // Increment failed attempts
            await _otpService.IncrementFailedAttemptsAsync(user.UserId, keyPrefix: "otp:reset:attempts:");
            var remainingAttempts = 5 - (failedAttempts + 1);
            throw new UnauthorizedAccessException($"Invalid OTP. {remainingAttempts} attempts remaining.");
        }

        // 4. Reset mật khẩu
        user.PasswordHash = HashPassword(request.NewPassword);
        await _unitOfWork.AuthenticationRepository.UpdateUserAsync(user);

        // 5. Xóa OTP và failed attempts
        await _otpService.RevokeOtpAsync(user.UserId, keyPrefix: "otp:reset:");
        await _otpService.ResetFailedAttemptsAsync(user.UserId, keyPrefix: "otp:reset:attempts:");

        // 6. Revoke tất cả token hiện tại (force logout)
        await RevokeTokenAsync(user.UserId);

        return true;
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        // Sử dụng BCrypt hoặc PBKDF2
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }

    public string HashPassword(string password)
    {
        // Sử dụng BCrypt
        return BCrypt.Net.BCrypt.HashPassword(password, 12);
    }

    #endregion

    #region Private Helpers

    private string GenerateJwtToken(Users user)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"] ?? ""));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role?.RoleName ?? "User"),
            new Claim("RoleId", user.RoleId.ToString())
        };

        // Thêm claim theo vai trò cụ thể
        if (user.Admins != null)
            claims.Add(new Claim("AdminId", user.Admins.AdminId.ToString()));
        if (user.Experts != null)
            claims.Add(new Claim("ExpertId", user.Experts.ExpertId.ToString()));
        if (user.Staffs != null)
            claims.Add(new Claim("StaffId", user.Staffs.StaffId.ToString()));
        if (user.Teachers != null)
            claims.Add(new Claim("TeacherId", user.Teachers.TeacherId.ToString()));

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(int.Parse(_configuration["Jwt:ExpiresInMinutes"] ?? "60")),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task SaveTokenToRedisAsync(int userId, string token, TimeSpan expiration)
    {
        var key = $"token:{userId}";
        await _redisDb.StringSetAsync(key, token, expiration);
        Console.WriteLine($"[REDIS] ✓ Saved token to Redis: {key} (expires in {expiration.TotalMinutes:F0} minutes)");
    }

    private string GenerateResetToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private UserInfo MapToUserInfo(Users user)
    {
        return new UserInfo
        {
            UserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            AvatarUrl = user.AvatarUrl,
            Status = user.Status ?? 1,
            Role = new RoleInfo
            {
                RoleId = user.Role?.RoleId ?? 0,
                RoleName = user.Role?.RoleName ?? "",
                Description = user.Role?.Description
            },
            AdminId = user.Admins?.AdminId,
            ExpertId = user.Experts?.ExpertId,
            StaffId = user.Staffs?.StaffId,
            TeacherId = user.Teachers?.TeacherId
        };
    }

    #endregion

    #region Change Password

    public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        // 1. Lấy thông tin user
        var user = await _unitOfWork.AuthenticationRepository.GetUserByIdAsync(userId);
        if (user == null)
            throw new UnauthorizedAccessException("User not found");

        // 2. Verify current password
        if (!VerifyPassword(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Current password is incorrect");

        // 3. Check if new password is same as current
        if (VerifyPassword(request.NewPassword, user.PasswordHash))
            throw new InvalidOperationException("New password must be different from current password");

        // 4. Update password
        user.PasswordHash = HashPassword(request.NewPassword);

        try
        {
            await _unitOfWork.AuthenticationRepository.UpdateUserAsync(user);
            await _unitOfWork.SaveChangesWithTransactionAsync();

            // 5. Revoke current token - force re-login with new password
            await RevokeTokenAsync(userId);

            return true;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to change password: {ex.Message}", ex);
        }
    }

    #endregion

    #region Email Verification

    #endregion
}