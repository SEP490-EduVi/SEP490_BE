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
            return Ok(ApiResponse<AuthResponse>.Success(result, "Đăng nhập thành công"));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Login failed for username: {Username}. Reason: {Reason}", request.Username, ex.Message);
            return Unauthorized(ApiResponse<AuthResponse>.Fail(ex.Message, 401));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for username: {Username}", request.Username);
            return StatusCode(500, ApiResponse<AuthResponse>.Fail("Lỗi đăng nhập, xin vui lòng thử lại (500)", 500));
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
            return Ok(ApiResponse<AuthResponse>.Success(result, "Đăng nhập bằng Google thành công"));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Google login failed. Reason: {Reason}", ex.Message);
            return Unauthorized(ApiResponse<AuthResponse>.Fail(ex.Message, 401));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Google login");
            return StatusCode(500, ApiResponse<AuthResponse>.Fail("Lỗi đăng nhập Google, xin vui lòng thử lại (500)", 500));
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
                $"Đăng ký thành công. Xin vui lòng kiểm tra mã OTP đã được gửi tới email {request.Email}.", 202));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Registration failed for email: {Email}. Reason: {Reason}", request.Email, ex.Message);
            return BadRequest(ApiResponse<RegisterResponse>.Fail($"Đăng ký thất bại cho email {request.Email}, xin vui lòng thử lại (400).", 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for email: {Email}", request.Email);
            return StatusCode(500, ApiResponse<RegisterResponse>.Fail("Lỗi đăng ký, xin vui lòng thử lại (500).", 500));
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
                "Xác nhận Email thành công. Bạn có thể đăng nhập."));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("OTP verification failed for userId: {UserId}. Reason: {Reason}", 
                request.UserId, ex.Message);
            return Unauthorized(ApiResponse<VerifyOtpResponse>.Fail("Xác nhận OTP không thành công, xin vui lòng thử lại.", 401));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Invalid OTP verification for userId: {UserId}. Reason: {Reason}", 
                request.UserId, ex.Message);
            return BadRequest(ApiResponse<VerifyOtpResponse>.Fail("Mã OTP sai, xin vui lòng thử lại.", 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OTP verification for userId: {UserId}", request.UserId);
            return StatusCode(500, ApiResponse<VerifyOtpResponse>.Fail("Lỗi xác nhận OTP, xin vui lòng thử lại (500).", 500));
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
                "Mã OTP đã được gửi lại, xin vui lòng kiểm tra email."));
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
            return StatusCode(500, ApiResponse<ResendOtpResponse>.Fail("Lỗi gửi lại OTP, xin vui lòng thử lại", 500));
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
                return Ok(ApiResponse<bool>.Success(true, "Đăng xuất thành công"));

            return BadRequest(ApiResponse<bool>.Fail("Đăng xuất thất bại", 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, ApiResponse<bool>.Fail("Lỗi đăng xuất, xin vui lòng thử lại", 500));
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
                return NotFound(ApiResponse<UserInfo>.Fail("Không tìm thấy người dùng", 404));

            return Ok(ApiResponse<UserInfo>.Success(result, "Lấy thông tin người dùng thành công"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current user information");
            return StatusCode(500, ApiResponse<UserInfo>.Fail("Lỗi khi lấy thông tin người dùng", 500));
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
            return Ok(ApiResponse<bool>.Success(true, "Nếu email tồn tại, mã OTP đã được gửi. OTP sẽ hết hạn sau 5 phút."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during forgot password for email: {Email}", request.Email);
            return StatusCode(500, ApiResponse<bool>.Fail("Lỗi xử lý yêu cầu, xin vui lòng thử lại", 500));
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
                "Mã OTP đã được gửi lại, xin vui lòng kiểm tra email."));
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
            return StatusCode(500, ApiResponse<ResendOtpResponse>.Fail("Lỗi gửi lại OTP, xin vui lòng thử lại", 500));
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
                return Ok(ApiResponse<bool>.Success(true, "Đặt lại mật khẩu thành công. Bạn có thể đăng nhập với mật khẩu mới."));

            return BadRequest(ApiResponse<bool>.Fail("Mã OTP không hợp lệ hoặc đã hết hạn", 400));
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
            return StatusCode(500, ApiResponse<bool>.Fail("Lỗi đặt lại mật khẩu, xin vui lòng thử lại", 500));
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
                return Unauthorized(ApiResponse<bool>.Fail("Không có token được cung cấp", 401));

            var isValid = await _authService.VerifySessionAsync(userId, token);

            if (isValid)
                return Ok(ApiResponse<bool>.Success(true, "Phiên đăng nhập hợp lệ"));

            return Unauthorized(ApiResponse<bool>.Fail("Phiên đăng nhập không hợp lệ", 401));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during session verification");
            return StatusCode(500, ApiResponse<bool>.Fail("Lỗi xác thực phiên đăng nhập, xin vui lòng thử lại", 500));
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
                return Ok(ApiResponse<bool>.Success(true, "Đổi mật khẩu thành công. Vui lòng đăng nhập lại với mật khẩu mới."));

            return BadRequest(ApiResponse<bool>.Fail("Đổi mật khẩu thất bại", 400));
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
            return StatusCode(500, ApiResponse<bool>.Fail("Lỗi đổi mật khẩu, xin vui lòng thử lại", 500));
        }
    }

    #region Private Helpers

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("Không tìm thấy người dùng");

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
