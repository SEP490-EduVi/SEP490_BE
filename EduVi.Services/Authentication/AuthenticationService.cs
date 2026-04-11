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
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        IConnectionMultiplexer redis,
        IRateLimitService rateLimitService,
        IEmailService emailService,
        IOtpService otpService,
        ILogger<AuthenticationService> logger)
    {
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _redis = redis;
        _redisDb = redis.GetDatabase();
        _rateLimitService = rateLimitService;
        _emailService = emailService;
        _otpService = otpService;
        _logger = logger;
    }

    #region Login & Register

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        // 0. Rate Limiting - 5 attempts per 5 minutes per username/email
        var rateLimitKey = $"login:{request.Username}";
        var isAllowed = await _rateLimitService.IsAllowedAsync(rateLimitKey, 5, 5);
        if (!isAllowed)
        {
            var remaining = await _rateLimitService.GetRemainingAttemptsAsync(rateLimitKey, 5, 5);
            throw new UnauthorizedAccessException($"Quá nhiều lần đăng nhập thất bại. Vui lòng thử lại sau 5 phút. Số lần còn lại: {remaining}");
        }

        // 1. Tìm người dùng theo username, nếu không có thì thử theo email
        var user = await _unitOfWork.AuthenticationRepository.GetUserByUsernameAsync(request.Username)
                   ?? await _unitOfWork.AuthenticationRepository.GetUserByEmailAsync(request.Username);
        if (user == null)
            throw new UnauthorizedAccessException("Đăng nhập thất bại. Tên đăng nhập hoặc mật khẩu không đúng");

        // 2. Kiểm tra mật khẩu
        if (!VerifyPassword(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Đăng nhập thất bại. Tên đăng nhập hoặc mật khẩu không đúng");

        // 3. Kiểm tra trạng thái tài khoản
        if (user.Status == 0) // Banned
            throw new UnauthorizedAccessException("Đăng nhập thất bại. Tài khoản của bạn đã bị khóa");

        // 4. CRITICAL: Check email verification status
        if (!user.IsEmailVerified)
            throw new UnauthorizedAccessException("Vui lòng xác thực email trước khi đăng nhập. Kiểm tra hộp thư đến để lấy mã OTP.");

        // 5. Login thành công - reset rate limit
        await _rateLimitService.ResetAsync(rateLimitKey);

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
                    RoleId = 4, // Default: Teacher role
                    Status = 1, // Active
                    IsEmailVerified = true, // Google email đã được verify
                    CreatedAt = DateTime.UtcNow
                };

                user = await _unitOfWork.AuthenticationRepository.CreateUserAsync(user);

                // Tự động tạo role-specific record (default Teacher)
                try
                {
                    var teacher = await _unitOfWork.AuthenticationRepository.CreateTeacherAsync(user.UserId);
                    await _unitOfWork.AuthenticationRepository.CreateWalletAsync(user.UserId);
                    // Cấp quota miễn phí ban đầu cho giáo viên mới qua Google
                    await _unitOfWork.PaymentRepository.CreateOrUpdateQuotaAsync(
                        teacher.TeacherId,
                        analysisQuotaToAdd: 2,
                        slideQuotaToAdd: 1,
                        videoQuotaToAdd: 1,
                        gameQuotaToAdd: 2);
                    await _unitOfWork.SaveChangesWithTransactionAsync();
                    _logger.LogInformation("Đã tạo hồ sơ Teacher, Wallet và Quota cho UserId={UserId} qua Google Login", user.UserId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Không thể tạo hồ sơ Teacher cho UserId={UserId} qua Google Login", user.UserId);
                    // Continue - user still logged in, can create record later
                }
            }

            // 3. Kiểm tra trạng thái
            if (user.Status == 0)
                throw new UnauthorizedAccessException("Đăng nhập thất bại. Tài khoản của bạn đã bị khóa");

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
            throw new UnauthorizedAccessException("Token Google không hợp lệ");
        }
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        // Defence-in-depth: enforce role whitelist even if DTO annotation is bypassed.
        // Admin (1) and Staff (2) must be created by an Admin, not via public sign-up.
        if (request.RoleId is not (3 or 4))
            throw new InvalidOperationException("Chỉ có vai trò Giáo viên hoặc Chuyên gia mới có thể tự đăng ký.");

        // 1. Kiểm tra email đã tồn tại
        if (await _unitOfWork.AuthenticationRepository.EmailExistsAsync(request.Email))
            throw new InvalidOperationException("Email này đã được sử dụng");

        // 2. Kiểm tra username đã tồn tại
        if (await _unitOfWork.AuthenticationRepository.UsernameExistsAsync(request.Username))
            throw new InvalidOperationException("Tên đăng nhập này đã được sử dụng");

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
            Status = 1, // Active nhưng chưa verify OTP
            IsEmailVerified = false, // QUAN TRỌNG: Chưa verify
            CreatedAt = DateTime.UtcNow
        };

        // Wrap User + role record creation in a single transaction so that a failure
        // in role record creation rolls back the Users row atomically — no orphaned rows.
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            user = await _unitOfWork.AuthenticationRepository.CreateUserAsync(user);

            switch (request.RoleId)
            {
                case 3: // Expert
                    await _unitOfWork.AuthenticationRepository.CreateExpertAsync(user.UserId);
                    break;

                case 4: // Teacher
                    var teacher = await _unitOfWork.AuthenticationRepository.CreateTeacherAsync(user.UserId);
                    // Cấp quota miễn phí ban đầu cho giáo viên mới
                    await _unitOfWork.PaymentRepository.CreateOrUpdateQuotaAsync(
                        teacher.TeacherId,
                        analysisQuotaToAdd: 2,
                        slideQuotaToAdd: 1,
                        videoQuotaToAdd: 1,
                        gameQuotaToAdd: 2);
                    break;
            }

            await _unitOfWork.AuthenticationRepository.CreateWalletAsync(user.UserId);
            await _unitOfWork.CommitTransactionAsync();
            _logger.LogInformation("User registered successfully. UserId={UserId}, RoleId={RoleId}", user.UserId, user.RoleId);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex, "Registration failed for email {Email} — transaction rolled back", request.Email);
            throw new InvalidOperationException($"Đăng ký thất bại: {ex.Message}", ex);
        }

        // 4. Generate OTP (6 digits)
        var otp = _otpService.GenerateOtp();
        
        // 5. Save OTP to Redis (5 minutes TTL)
        await _otpService.SaveOtpAsync(user.UserId, otp, ttlMinutes: 5);

        // 6. Send OTP email (async, non-blocking)
        try
        {
            await _emailService.SendEmailVerificationAsync(user.Email, otp, user.FullName);
            _logger.LogInformation("Đã gửi email OTP đến {Email}", user.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể gửi email OTP đến {Email}", user.Email);
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
            throw new UnauthorizedAccessException("Quá nhiều lần nhập sai. Vui lòng yêu cầu mã OTP mới.");

        // 2. Verify OTP from Redis
        var isValid = await _otpService.VerifyOtpAsync(request.UserId, request.Otp);
        
        if (!isValid)
        {
            // Increment failed attempts
            await _otpService.IncrementFailedAttemptsAsync(request.UserId);
            throw new UnauthorizedAccessException("Mã OTP không hợp lệ hoặc đã hết hạn");
        }

        // 3. Get user from DB
        var user = await _unitOfWork.AuthenticationRepository.GetUserByIdAsync(request.UserId);
        if (user == null)
            throw new InvalidOperationException("Không tìm thấy người dùng");

        // 4. Update user status
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

            _logger.LogInformation("Đã xác thực email thành công cho UserId={UserId}", request.UserId);

            // 6. Gửi welcome email (async, non-blocking)
            var roleName = user.Role?.RoleName ?? "user";
            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailService.SendWelcomeEmailAsync(user.Email, user.FullName, roleName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Không thể gửi welcome email đến {Email}", user.Email);
                }
            });

            return new VerifyOtpResponse
            {
                UserId = user.UserId,
                Email = user.Email,
                IsVerified = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể cập nhật trạng thái xác thực email cho UserId={UserId}", request.UserId);
            throw new Exception("Xác thực email thất bại. Vui lòng thử lại.", ex);
        }
    }

    public async Task<ResendOtpResponse> ResendOtpAsync(ResendOtpRequest request)
    {
        // 1. Check if user exists
        var user = await _unitOfWork.AuthenticationRepository.GetUserByIdAsync(request.UserId);
        if (user == null)
            throw new InvalidOperationException("Không tìm thấy người dùng");

        // 2. Check if already verified
        if (user.IsEmailVerified)
            throw new InvalidOperationException("Email này đã được xác thực");

        // 3. Check cooldown (60 seconds)
        var canResend = await _otpService.CanResendOtpAsync(request.UserId);
        if (!canResend)
            throw new InvalidOperationException("Vui lòng chờ 60 giây trước khi yêu cầu mã OTP mới");

        // 4. Check daily limit (5 times per day)
        var resendCount = await _otpService.GetResendCountAsync(request.UserId);
        if (resendCount >= 5)
            throw new InvalidOperationException("Đã đạt giới hạn gửi lại tối đa (5 lần/ngày). Vui lòng thử lại vào ngày mai.");

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
            await _emailService.SendEmailVerificationAsync(user.Email, otp, user.FullName);
            _logger.LogInformation("Đã gửi lại OTP đến {Email} ({Count}/5)", user.Email, resendCount + 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể gửi lại email OTP đến {Email}", user.Email);
            throw new Exception("Không thể gửi email OTP", ex);
        }

        return new ResendOtpResponse
        {
            CanResendAgainAt = DateTime.UtcNow.AddSeconds(60)
        };
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

    public async Task<UserInfo> UpdateCurrentUserAsync(int userId, UpdateCurrentUserRequest request)
    {
        var user = await _unitOfWork.AuthenticationRepository.GetUserByIdAsync(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy người dùng");

        var hasAnyFieldToUpdate = request.FullName is not null || request.PhoneNumber is not null || request.AvatarUrl is not null;
        if (!hasAnyFieldToUpdate)
            throw new InvalidOperationException("Không có thông tin nào để cập nhật");

        if (request.FullName is not null)
            user.FullName = request.FullName.Trim();

        if (request.PhoneNumber is not null)
            user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();

        if (request.AvatarUrl is not null)
            user.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();

        await _unitOfWork.AuthenticationRepository.UpdateUserAsync(user);

        var updatedUser = await _unitOfWork.AuthenticationRepository.GetUserByIdAsync(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy người dùng");

        return MapToUserInfo(updatedUser);
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
            await _emailService.SendPasswordResetEmailAsync(user.Email, otp, user.FullName);
            _logger.LogInformation("Đã gửi OTP đặt lại mật khẩu đến {Email}", user.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể gửi email OTP đặt lại mật khẩu đến {Email}", user.Email);
            // Continue execution - OTP is already saved in Redis
        }

        return true;
    }

    public async Task<ResendOtpResponse> ResendResetPasswordOtpAsync(string email)
    {
        // 1. Tìm người dùng
        var user = await _unitOfWork.AuthenticationRepository.GetUserByEmailAsync(email);
        if (user == null)
            throw new UnauthorizedAccessException("Không tìm thấy người dùng");

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
            throw new InvalidOperationException("Bạn đã đạt giới hạn gửi lại tối đa trong ngày (5 lần). Vui lòng thử lại vào ngày mai.");
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
            await _emailService.SendPasswordResetEmailAsync(user.Email, otp, user.FullName);
            _logger.LogInformation("Đã gửi lại OTP đặt lại mật khẩu đến {Email}", user.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể gửi lại email OTP đặt lại mật khẩu đến {Email}", user.Email);
        }

        return new ResendOtpResponse
        {
            CanResendAgainAt = DateTimeOffset.UtcNow.AddSeconds(60)
        };
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
    {
        // 1. Tìm người dùng
        var user = await _unitOfWork.AuthenticationRepository.GetUserByEmailAsync(request.Email);
        if (user == null)
            throw new UnauthorizedAccessException("Không tìm thấy người dùng");

        // 2. Check brute-force protection
        var failedAttempts = await _otpService.GetFailedAttemptsAsync(user.UserId, keyPrefix: "otp:reset:attempts:");
        if (failedAttempts >= 5)
        {
            throw new UnauthorizedAccessException("Quá nhiều lần nhập sai mã OTP. Vui lòng yêu cầu mã OTP mới.");
        }

        // 3. Verify OTP từ Redis
        var isValid = await _otpService.VerifyOtpAsync(user.UserId, request.Otp, keyPrefix: "otp:reset:");
        if (!isValid)
        {
            // Increment failed attempts
            await _otpService.IncrementFailedAttemptsAsync(user.UserId, keyPrefix: "otp:reset:attempts:");
            var remainingAttempts = 5 - (failedAttempts + 1);
            throw new UnauthorizedAccessException($"Mã OTP không hợp lệ. Còn {remainingAttempts} lần thử.");
        }

        // 4. Reset mật khẩu
        user.PasswordHash = HashPassword(request.NewPassword);
        await _unitOfWork.AuthenticationRepository.UpdateUserAsync(user);
        await _unitOfWork.SaveChangesWithTransactionAsync();

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
        {
            claims.Add(new Claim("ExpertId", user.Experts.ExpertId.ToString()));
            // expert_is_verified giúp API gate các endpoint Expert chỉ cho phép khi đã được Staff duyệt
            // null → "pending", true → "true", false → "false"
            var verifiedValue = user.Experts.IsVerified.HasValue
                ? user.Experts.IsVerified.Value.ToString().ToLower()
                : "pending";
            claims.Add(new Claim("expert_is_verified", verifiedValue));
        }
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
        _logger.LogInformation("Đã lưu token vào Redis cho {Key}, hết hạn sau {Minutes} phút", key, (int)expiration.TotalMinutes);
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
            UserCode = user.UserCode ?? string.Empty,
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
            TeacherId = user.Teachers?.TeacherId,
            ExpertIsVerified = user.Experts?.IsVerified
        };
    }

    #endregion

    #region Change Password

    public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        // 1. Lấy thông tin user
        var user = await _unitOfWork.AuthenticationRepository.GetUserByIdAsync(userId);
        if (user == null)
            throw new UnauthorizedAccessException("Không tìm thấy người dùng");

        // 2. Verify current password
        if (!VerifyPassword(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Mật khẩu hiện tại không đúng");

        // 3. Check if new password is same as current
        if (VerifyPassword(request.NewPassword, user.PasswordHash))
            throw new InvalidOperationException("Mật khẩu mới phải khác mật khẩu hiện tại");

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
            throw new Exception($"Đổi mật khẩu thất bại: {ex.Message}", ex);
        }
    }

    #endregion

    #region Email Verification

    #endregion
}