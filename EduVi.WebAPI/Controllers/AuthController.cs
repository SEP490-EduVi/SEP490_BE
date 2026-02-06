using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Authentication.Request;
using EduVi.Contracts.DTOs.Authentication.Response;
using EduVi.Services.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EduVi.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthenticationService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Đăng nhập truyền thống với Username/Password
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var result = await _authService.LoginAsync(request);
            return Ok(ApiResponse<AuthResponse>.Success(result, "Login successful"));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Login failed for username: {Username}. Reason: {Reason}", request.Username, ex.Message);
            return Unauthorized(ApiResponse<AuthResponse>.Fail(ex.Message, 401));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for username: {Username}", request.Username);
            return StatusCode(500, ApiResponse<AuthResponse>.Fail("An error occurred during login", 500));
        }
    }

    /// <summary>
    /// Đăng nhập với Google
    /// </summary>
    [HttpPost("google-login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        try
        {
            var result = await _authService.GoogleLoginAsync(request);
            return Ok(ApiResponse<AuthResponse>.Success(result, "Google login successful"));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Google login failed. Reason: {Reason}", ex.Message);
            return Unauthorized(ApiResponse<AuthResponse>.Fail(ex.Message, 401));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Google login");
            return StatusCode(500, ApiResponse<AuthResponse>.Fail("An error occurred during Google login", 500));
        }
    }

    /// <summary>
    /// Đăng ký tài khoản mới (Teacher/Expert) - KHÔNG cho login ngay, phải verify OTP trước
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<RegisterResponse>>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var result = await _authService.RegisterAsync(request);
            return StatusCode(202, ApiResponse<RegisterResponse>.Success(result, 
                "Registration successful. Please check your email for OTP verification.", 202));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Registration failed for email: {Email}. Reason: {Reason}", request.Email, ex.Message);
            return BadRequest(ApiResponse<RegisterResponse>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for email: {Email}", request.Email);
            return StatusCode(500, ApiResponse<RegisterResponse>.Fail("An error occurred during registration", 500));
        }
    }

    /// <summary>
    /// Verify OTP sau khi register - Account được activate sau bước này
    /// </summary>
    [HttpPost("verify-otp")]
    public async Task<ActionResult<ApiResponse<VerifyOtpResponse>>> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        try
        {
            var result = await _authService.VerifyOtpAsync(request);
            return Ok(ApiResponse<VerifyOtpResponse>.Success(result, 
                "Email verified successfully. You can now login."));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("OTP verification failed for userId: {UserId}. Reason: {Reason}", 
                request.UserId, ex.Message);
            return Unauthorized(ApiResponse<VerifyOtpResponse>.Fail(ex.Message, 401));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Invalid OTP verification for userId: {UserId}. Reason: {Reason}", 
                request.UserId, ex.Message);
            return BadRequest(ApiResponse<VerifyOtpResponse>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OTP verification for userId: {UserId}", request.UserId);
            return StatusCode(500, ApiResponse<VerifyOtpResponse>.Fail("An error occurred during verification", 500));
        }
    }

    /// <summary>
    /// Resend OTP với rate limiting (60s cooldown, max 5/day)
    /// </summary>
    [HttpPost("resend-otp")]
    public async Task<ActionResult<ApiResponse<ResendOtpResponse>>> ResendOtp([FromBody] ResendOtpRequest request)
    {
        try
        {
            var result = await _authService.ResendOtpAsync(request);
            return Ok(ApiResponse<ResendOtpResponse>.Success(result, 
                "OTP resent successfully. Please check your email."));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Resend OTP failed for userId: {UserId}. Reason: {Reason}", 
                request.UserId, ex.Message);
            return BadRequest(ApiResponse<ResendOtpResponse>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resending OTP for userId: {UserId}", request.UserId);
            return StatusCode(500, ApiResponse<ResendOtpResponse>.Fail("An error occurred while resending OTP", 500));
        }
    }

    /// <summary>
    /// Đăng xuất - Xóa phiên làm việc trên Redis
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<bool>>> Logout()
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _authService.LogoutAsync(userId);

            if (result)
                return Ok(ApiResponse<bool>.Success(true, "Logout successful"));

            return BadRequest(ApiResponse<bool>.Fail("Logout failed", 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, ApiResponse<bool>.Fail("An error occurred during logout", 500));
        }
    }

    /// <summary>
    /// Lấy thông tin người dùng hiện tại (Identity Info)
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserInfo>>> GetCurrentUser()
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _authService.GetCurrentUserAsync(userId);

            if (result == null)
                return NotFound(ApiResponse<UserInfo>.Fail("User not found", 404));

            return Ok(ApiResponse<UserInfo>.Success(result, "User information retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current user information");
            return StatusCode(500, ApiResponse<UserInfo>.Fail("An error occurred while retrieving user information", 500));
        }
    }

    /// <summary>
    /// Gửi OTP reset mật khẩu qua email (6 digits, 5 phút)
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<ActionResult<ApiResponse<bool>>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        try
        {
            var result = await _authService.ForgotPasswordAsync(request);
            
            // Luôn trả về success để không tiết lộ email có tồn tại hay không
            return Ok(ApiResponse<bool>.Success(true, "If the email exists, an OTP has been sent. The OTP will expire in 5 minutes."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during forgot password for email: {Email}", request.Email);
            return StatusCode(500, ApiResponse<bool>.Fail("An error occurred while processing your request", 500));
        }
    }

    /// <summary>
    /// Gửi lại OTP reset mật khẩu (rate limit: 60s cooldown, max 5/day)
    /// </summary>
    [HttpPost("resend-reset-otp")]
    public async Task<ActionResult<ApiResponse<ResendOtpResponse>>> ResendResetOtp([FromBody] ForgotPasswordRequest request)
    {
        try
        {
            var result = await _authService.ResendResetPasswordOtpAsync(request.Email);
            return Ok(ApiResponse<ResendOtpResponse>.Success(result, 
                "OTP resent successfully. Please check your email."));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Resend reset OTP failed for email: {Email}. Reason: {Reason}", 
                request.Email, ex.Message);
            return BadRequest(ApiResponse<ResendOtpResponse>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resending reset OTP for email: {Email}", request.Email);
            return StatusCode(500, ApiResponse<ResendOtpResponse>.Fail("An error occurred while resending OTP", 500));
        }
    }

    /// <summary>
    /// Reset mật khẩu với OTP (6 digits)
    /// </summary>
    [HttpPost("reset-password")]
    public async Task<ActionResult<ApiResponse<bool>>> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            var result = await _authService.ResetPasswordAsync(request);

            if (result)
                return Ok(ApiResponse<bool>.Success(true, "Password reset successful. You can now login with your new password."));

            return BadRequest(ApiResponse<bool>.Fail("Invalid or expired OTP", 400));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Reset password failed for email: {Email}. Reason: {Reason}", 
                request.Email, ex.Message);
            return Unauthorized(ApiResponse<bool>.Fail(ex.Message, 401));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during password reset for email: {Email}", request.Email);
            return StatusCode(500, ApiResponse<bool>.Fail("An error occurred while resetting password", 500));
        }
    }

    /// <summary>
    /// Xác thực session (Dùng cho middleware hoặc health check)
    /// </summary>
    [HttpPost("verify-session")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<bool>>> VerifySession()
    {
        try
        {
            var userId = GetCurrentUserId();
            var token = GetTokenFromHeader();

            if (string.IsNullOrEmpty(token))
                return Unauthorized(ApiResponse<bool>.Fail("Token not provided", 401));

            var isValid = await _authService.VerifySessionAsync(userId, token);

            if (isValid)
                return Ok(ApiResponse<bool>.Success(true, "Session is valid"));

            return Unauthorized(ApiResponse<bool>.Fail("Invalid session", 401));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during session verification");
            return StatusCode(500, ApiResponse<bool>.Fail("An error occurred during session verification", 500));
        }
    }

    /// <summary>
    /// Đổi mật khẩu cho user đã đăng nhập
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<bool>>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _authService.ChangePasswordAsync(userId, request);

            if (success)
                return Ok(ApiResponse<bool>.Success(true, "Password changed successfully. Please login again with new password."));

            return BadRequest(ApiResponse<bool>.Fail("Failed to change password", 400));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized password change attempt");
            return Unauthorized(ApiResponse<bool>.Fail(ex.Message, 401));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid password change operation");
            return BadRequest(ApiResponse<bool>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during password change");
            return StatusCode(500, ApiResponse<bool>.Fail("An error occurred during password change", 500));
        }
    }

    #region Private Helpers

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("User ID not found in token");

        return userId;
    }

    private string? GetTokenFromHeader()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return null;

        return authHeader.Substring("Bearer ".Length).Trim();
    }

    #endregion
}
