using EduVi.Contracts.DTOs.Authentication.Request;
using EduVi.Contracts.DTOs.Authentication.Response;

namespace EduVi.Services.Authentication;

public interface IAuthenticationService
{
    /// <summary>
    /// Đăng nhập truyền thống với Username/Password
    /// </summary>
    Task<AuthResponse> LoginAsync(LoginRequest request);

    /// <summary>
    /// Đăng nhập với Google
    /// </summary>
    Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest request);

    /// <summary>
    /// Đăng ký tài khoản mới với OTP verification (không cho login ngay)
    /// </summary>
    Task<RegisterResponse> RegisterAsync(RegisterRequest request);
    
    /// <summary>
    /// Verify OTP sau khi register
    /// </summary>
    Task<VerifyOtpResponse> VerifyOtpAsync(VerifyOtpRequest request);
    
    /// <summary>
    /// Resend OTP với rate limiting
    /// </summary>
    Task<ResendOtpResponse> ResendOtpAsync(ResendOtpRequest request);

    /// <summary>
    /// Đăng xuất - Xóa phiên làm việc trên Redis
    /// </summary>
    Task<bool> LogoutAsync(int userId);

    /// <summary>
    /// Xác thực Token từ Redis
    /// </summary>
    Task<bool> VerifySessionAsync(int userId, string token);

    /// <summary>
    /// Thu hồi Token khi bị Ban
    /// </summary>
    Task<bool> RevokeTokenAsync(int userId);

    /// <summary>
    /// Lấy thông tin người dùng hiện tại
    /// </summary>
    Task<UserInfo?> GetCurrentUserAsync(int userId);

    /// <summary>
    /// Gửi OTP reset mật khẩu qua email (6 digits, 5 min TTL)
    /// </summary>
    Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request);

    /// <summary>
    /// Resend OTP cho password reset với rate limiting (60s cooldown, 5/day)
    /// </summary>
    Task<ResendOtpResponse> ResendResetPasswordOtpAsync(string email);

    /// <summary>
    /// Reset mật khẩu với OTP
    /// </summary>
    Task<bool> ResetPasswordAsync(ResetPasswordRequest request);

    /// <summary>
    /// Đổi mật khẩu cho user đã đăng nhập
    /// </summary>
    Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request);

    /// <summary>
    /// Xác thực mật khẩu
    /// </summary>
    bool VerifyPassword(string password, string passwordHash);

    /// <summary>
    /// Băm mật khẩu
    /// </summary>
    string HashPassword(string password);
}
