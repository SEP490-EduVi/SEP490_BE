namespace EduVi.Services.Otp;

public interface IOtpService
{
    /// <summary>
    /// Generate 6-digit numeric OTP
    /// </summary>
    string GenerateOtp();
    
    /// <summary>
    /// Save OTP to Redis with TTL and custom key prefix
    /// </summary>
    Task SaveOtpAsync(int userId, string otp, int ttlMinutes = 5, string keyPrefix = "otp:verify:");
    
    /// <summary>
    /// Verify OTP from Redis with custom key prefix
    /// </summary>
    Task<bool> VerifyOtpAsync(int userId, string otp, string keyPrefix = "otp:verify:");
    
    /// <summary>
    /// Check if can resend OTP (60 seconds cooldown) with custom key prefix
    /// </summary>
    Task<bool> CanResendOtpAsync(int userId, string keyPrefix = "otp:resend:cooldown:");
    
    /// <summary>
    /// Get resend count for rate limiting (max 5/day) with custom key prefix
    /// </summary>
    Task<int> GetResendCountAsync(int userId, string keyPrefix = "otp:resend:limit:");
    
    /// <summary>
    /// Increment resend counter with custom key prefix
    /// </summary>
    Task IncrementResendCountAsync(int userId, string keyPrefix = "otp:resend:limit:");
    
    /// <summary>
    /// Get failed attempts count with custom key prefix
    /// </summary>
    Task<int> GetFailedAttemptsAsync(int userId, string keyPrefix = "otp:attempts:");
    
    /// <summary>
    /// Increment failed attempts (for brute-force protection) with custom key prefix
    /// </summary>
    Task IncrementFailedAttemptsAsync(int userId, string keyPrefix = "otp:attempts:");
    
    /// <summary>
    /// Reset failed attempts after successful verification with custom key prefix
    /// </summary>
    Task ResetFailedAttemptsAsync(int userId, string keyPrefix = "otp:attempts:");
    
    /// <summary>
    /// Revoke OTP (delete from Redis) with custom key prefix
    /// </summary>
    Task RevokeOtpAsync(int userId, string keyPrefix = "otp:verify:");
}
