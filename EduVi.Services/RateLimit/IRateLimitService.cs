namespace EduVi.Services.RateLimit;

public interface IRateLimitService
{
    /// <summary>
    /// Check if request is allowed under rate limit
    /// </summary>
    /// <param name="key">Unique identifier (e.g., username, IP)</param>
    /// <param name="maxAttempts">Maximum allowed attempts</param>
    /// <param name="windowMinutes">Time window in minutes</param>
    /// <returns>True if allowed, false if rate limit exceeded</returns>
    Task<bool> IsAllowedAsync(string key, int maxAttempts, int windowMinutes);
    
    /// <summary>
    /// Get remaining attempts for a key
    /// </summary>
    Task<int> GetRemainingAttemptsAsync(string key, int maxAttempts, int windowMinutes);
    
    /// <summary>
    /// Reset rate limit for a key (e.g., after successful login)
    /// </summary>
    Task ResetAsync(string key);
}
