using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Security.Cryptography;

namespace EduVi.Services.Otp;

public class OtpService : IOtpService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _redisDb;
    private readonly ILogger<OtpService> _logger;

    // Configuration constants
    private const int OTP_LENGTH = 6;
    private const int OTP_TTL_MINUTES = 5;
    private const int RESEND_COOLDOWN_SECONDS = 60;
    private const int MAX_RESEND_PER_DAY = 5;
    private const int MAX_FAILED_ATTEMPTS = 5;
    private const int FAILED_ATTEMPTS_TTL_MINUTES = 15;

    public OtpService(IConnectionMultiplexer redis, ILogger<OtpService> logger)
    {
        _redis = redis;
        _redisDb = redis.GetDatabase();
        _logger = logger;
    }

    public string GenerateOtp()
    {
        // Use cryptographically secure random for production
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[4];
        rng.GetBytes(bytes);
        
        // Convert to 6-digit number (000000-999999)
        var num = BitConverter.ToUInt32(bytes, 0) % 1000000;
        return num.ToString("D6");
    }

    public async Task SaveOtpAsync(int userId, string otp, int ttlMinutes = OTP_TTL_MINUTES, string keyPrefix = "otp:verify:")
    {
        try
        {
            var key = $"{keyPrefix}{userId}";
            await _redisDb.StringSetAsync(key, otp, TimeSpan.FromMinutes(ttlMinutes));
            _logger.LogInformation("[OTP] ✓ OTP saved for userId {UserId}, expires in {Minutes}m", userId, ttlMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OTP] ✗ Failed to save OTP for userId {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> VerifyOtpAsync(int userId, string otp, string keyPrefix = "otp:verify:")
    {
        try
        {
            var key = $"{keyPrefix}{userId}";
            var storedOtp = await _redisDb.StringGetAsync(key);

            if (!storedOtp.HasValue)
            {
                _logger.LogWarning("[OTP] OTP not found or expired for userId {UserId}", userId);
                return false;
            }

            var isValid = storedOtp.ToString() == otp;
            
            if (isValid)
            {
                _logger.LogInformation("[OTP] ✓ OTP verified successfully for userId {UserId}", userId);
                // Don't delete here - let caller decide after DB update succeeds
            }
            else
            {
                _logger.LogWarning("[OTP] ✗ Invalid OTP for userId {UserId}", userId);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OTP] Error verifying OTP for userId {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> CanResendOtpAsync(int userId, string keyPrefix = "otp:resend:cooldown:")
    {
        try
        {
            var cooldownKey = $"{keyPrefix}{userId}";
            var exists = await _redisDb.KeyExistsAsync(cooldownKey);
            
            if (exists)
            {
                var ttl = await _redisDb.KeyTimeToLiveAsync(cooldownKey);
                _logger.LogWarning("[OTP] Resend cooldown active for userId {UserId}, {Seconds}s remaining", 
                    userId, ttl?.TotalSeconds ?? 0);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OTP] Error checking resend cooldown for userId {UserId}", userId);
            return false;
        }
    }

    public async Task<int> GetResendCountAsync(int userId, string keyPrefix = "otp:resend:limit:")
    {
        try
        {
            var key = $"{keyPrefix}{userId}";
            var count = await _redisDb.StringGetAsync(key);
            return count.HasValue ? (int)count : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OTP] Error getting resend count for userId {UserId}", userId);
            return 0;
        }
    }

    public async Task IncrementResendCountAsync(int userId, string keyPrefix = "otp:resend:limit:")
    {
        try
        {
            var key = $"{keyPrefix}{userId}";
            var count = await _redisDb.StringIncrementAsync(key);
            
            // Set TTL on first increment
            if (count == 1)
            {
                await _redisDb.KeyExpireAsync(key, TimeSpan.FromHours(24));
            }

            // Set cooldown - extract prefix from keyPrefix (otp:reset:resend:limit: -> otp:reset:resend:cooldown:)
            var basePath = keyPrefix.Replace(":limit:", ":cooldown:");
            var cooldownKey = $"{basePath}{userId}";
            await _redisDb.StringSetAsync(cooldownKey, "1", TimeSpan.FromSeconds(RESEND_COOLDOWN_SECONDS));

            _logger.LogInformation("[OTP] Resend count incremented to {Count} for userId {UserId}", count, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OTP] Error incrementing resend count for userId {UserId}", userId);
            throw;
        }
    }

    public async Task<int> GetFailedAttemptsAsync(int userId, string keyPrefix = "otp:attempts:")
    {
        try
        {
            var key = $"{keyPrefix}{userId}";
            var count = await _redisDb.StringGetAsync(key);
            return count.HasValue ? (int)count : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OTP] Error getting failed attempts for userId {UserId}", userId);
            return 0;
        }
    }

    public async Task IncrementFailedAttemptsAsync(int userId, string keyPrefix = "otp:attempts:")
    {
        try
        {
            var key = $"{keyPrefix}{userId}";
            var count = await _redisDb.StringIncrementAsync(key);
            
            // Set TTL on first increment
            if (count == 1)
            {
                await _redisDb.KeyExpireAsync(key, TimeSpan.FromMinutes(FAILED_ATTEMPTS_TTL_MINUTES));
            }

            _logger.LogWarning("[OTP] Failed attempts: {Count}/{Max} for userId {UserId}", 
                count, MAX_FAILED_ATTEMPTS, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OTP] Error incrementing failed attempts for userId {UserId}", userId);
        }
    }

    public async Task ResetFailedAttemptsAsync(int userId, string keyPrefix = "otp:attempts:")
    {
        try
        {
            var key = $"{keyPrefix}{userId}";
            await _redisDb.KeyDeleteAsync(key);
            _logger.LogInformation("[OTP] Failed attempts reset for userId {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OTP] Error resetting failed attempts for userId {UserId}", userId);
        }
    }

    public async Task RevokeOtpAsync(int userId, string keyPrefix = "otp:verify:")
    {
        try
        {
            var key = $"{keyPrefix}{userId}";
            await _redisDb.KeyDeleteAsync(key);
            _logger.LogInformation("[OTP] OTP revoked for userId {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OTP] Error revoking OTP for userId {UserId}", userId);
        }
    }
}
