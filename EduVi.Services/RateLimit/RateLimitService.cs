using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace EduVi.Services.RateLimit;

public class RateLimitService : IRateLimitService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RateLimitService> _logger;

    public RateLimitService(IConnectionMultiplexer redis, ILogger<RateLimitService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<bool> IsAllowedAsync(string key, int maxAttempts, int windowMinutes)
    {
        try
        {
            var db = _redis.GetDatabase();
            var redisKey = $"ratelimit:{key}";
            
            var currentCount = await db.StringGetAsync(redisKey);
            
            if (!currentCount.HasValue)
            {
                // First attempt - set counter with expiration
                await db.StringSetAsync(redisKey, 1, TimeSpan.FromMinutes(windowMinutes));
                _logger.LogInformation("Rate limit initialized for {Key}: 1/{MaxAttempts}", key, maxAttempts);
                return true;
            }

            var count = (int)currentCount;
            
            if (count >= maxAttempts)
            {
                var ttl = await db.KeyTimeToLiveAsync(redisKey);
                _logger.LogWarning("Rate limit exceeded for {Key}: {Count}/{MaxAttempts}. TTL: {TTL}", 
                    key, count, maxAttempts, ttl);
                return false;
            }

            // Increment counter
            await db.StringIncrementAsync(redisKey);
            _logger.LogInformation("Rate limit check for {Key}: {Count}/{MaxAttempts}", key, count + 1, maxAttempts);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis error in rate limiting for {Key}. Allowing request.", key);
            // On Redis failure, allow the request (fail open)
            return true;
        }
    }

    public async Task<int> GetRemainingAttemptsAsync(string key, int maxAttempts, int windowMinutes)
    {
        try
        {
            var db = _redis.GetDatabase();
            var redisKey = $"ratelimit:{key}";
            
            var currentCount = await db.StringGetAsync(redisKey);
            
            if (!currentCount.HasValue)
                return maxAttempts;

            var count = (int)currentCount;
            return Math.Max(0, maxAttempts - count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis error getting remaining attempts for {Key}", key);
            return maxAttempts; // Assume full attempts on error
        }
    }

    public async Task ResetAsync(string key)
    {
        try
        {
            var db = _redis.GetDatabase();
            var redisKey = $"ratelimit:{key}";
            await db.KeyDeleteAsync(redisKey);
            _logger.LogInformation("Rate limit reset for {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis error resetting rate limit for {Key}", key);
        }
    }
}
