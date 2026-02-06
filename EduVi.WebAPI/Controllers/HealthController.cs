using EduVi.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System.Security.Claims;

namespace EduVi.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<HealthController> _logger;

    public HealthController(IConnectionMultiplexer redis, ILogger<HealthController> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    /// <summary>
    /// Kiểm tra trạng thái Redis - Test read/write
    /// </summary>
    [HttpGet("redis")]
    public ActionResult<ApiResponse<object>> CheckRedis()
    {
        try
        {
            var db = _redis.GetDatabase();
            
            // Test write
            var testKey = "health:check";
            var testValue = $"test_{DateTime.UtcNow:yyyyMMddHHmmss}";
            db.StringSet(testKey, testValue, TimeSpan.FromSeconds(10));
            
            // Test read
            var readValue = db.StringGet(testKey);
            
            if (readValue.HasValue && readValue.ToString() == testValue)
            {
                var info = new
                {
                    Status = "Connected ✓",
                    IsConnected = _redis.IsConnected,
                    TestWrite = "Success",
                    TestRead = "Success",
                    Endpoints = _redis.GetEndPoints().Select(e => e.ToString()).ToArray(),
                    Message = "Redis đang hoạt động bình thường"
                };
                
                _logger.LogInformation("✓ Redis health check passed");
                return Ok(ApiResponse<object>.Success(info, "Redis is healthy"));
            }
            else
            {
                return StatusCode(500, ApiResponse<object>.Fail("Redis read/write test failed", 500));
            }
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "✗ Redis connection failed");
            
            var error = new
            {
                Status = "Disconnected ✗",
                IsConnected = false,
                Error = ex.Message,
                Message = "Redis chưa được khởi động",
                Instructions = new[]
                {
                    "1. Mở Docker Desktop",
                    "2. Chạy: docker run -d -p 6379:6379 --name redis-eduvi redis:latest",
                    "3. Kiểm tra: docker ps"
                }
            };
            
            var response = ApiResponse<object>.Fail("Redis is not available", 503);
            response.Result = error;
            return StatusCode(503, response);
        }
    }

    /// <summary>
    /// Xem tất cả sessions đang active trong Redis (CHỈ DÙNG CHO DEBUG)
    /// </summary>
    [HttpGet("redis/sessions")]
    [Authorize] // Chỉ admin/user đã login mới xem được
    public async Task<ActionResult<ApiResponse<object>>> GetAllSessions()
    {
        try
        {
            var db = _redis.GetDatabase();
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            
            // Lấy tất cả keys pattern "token:*"
            var tokenKeys = server.Keys(pattern: "token:*").ToList();
            
            var sessions = new List<object>();
            foreach (var key in tokenKeys)
            {
                var token = await db.StringGetAsync(key);
                var ttl = await db.KeyTimeToLiveAsync(key);
                
                var userId = key.ToString().Replace("token:", "");
                sessions.Add(new
                {
                    UserId = userId,
                    Key = key.ToString(),
                    HasToken = token.HasValue,
                    TokenPreview = token.HasValue ? token.ToString().Substring(0, Math.Min(20, token.ToString().Length)) + "..." : null,
                    ExpiresIn = ttl?.TotalMinutes.ToString("F0") + " minutes" ?? "No expiration"
                });
            }
            
            var result = new
            {
                TotalSessions = sessions.Count,
                Sessions = sessions,
                Message = sessions.Count > 0 
                    ? $"Có {sessions.Count} sessions đang active trong Redis" 
                    : "Không có sessions nào trong Redis"
            };
            
            _logger.LogInformation("Retrieved {Count} sessions from Redis", sessions.Count);
            return Ok(ApiResponse<object>.Success(result, "Sessions retrieved"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Redis sessions");
            return StatusCode(500, ApiResponse<object>.Fail($"Error: {ex.Message}", 500));
        }
    }

    /// <summary>
    /// Xem session của user hiện tại
    /// </summary>
    [HttpGet("redis/my-session")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> GetMySession()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.Fail("User ID not found", 401));
            
            var db = _redis.GetDatabase();
            var key = $"token:{userId}";
            var token = await db.StringGetAsync(key);
            var ttl = await db.KeyTimeToLiveAsync(key);
            
            if (token.HasValue)
            {
                var result = new
                {
                    UserId = userId,
                    RedisKey = key,
                    HasToken = true,
                    TokenPreview = token.ToString().Substring(0, Math.Min(30, token.ToString().Length)) + "...",
                    ExpiresIn = ttl?.TotalMinutes.ToString("F0") + " minutes",
                    Status = "✓ Token đang được lưu trong Redis",
                    Message = "Session của bạn đang active và hợp lệ"
                };
                
                return Ok(ApiResponse<object>.Success(result, "Session found"));
            }
            else
            {
                var result = new
                {
                    UserId = userId,
                    RedisKey = key,
                    HasToken = false,
                    Status = "✗ Token không tồn tại trong Redis",
                    Message = "Session đã hết hạn hoặc đã logout"
                };
                
                return Ok(ApiResponse<object>.Success(result, "No session found"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user session");
            return StatusCode(500, ApiResponse<object>.Fail($"Error: {ex.Message}", 500));
        }
    }

    /// <summary>
    /// Xóa tất cả sessions trong Redis (CLEAR ALL - CHỈ DÙNG CHO DEBUG)
    /// </summary>
    [HttpDelete("redis/sessions")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> ClearAllSessions()
    {
        try
        {
            var db = _redis.GetDatabase();
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            
            var tokenKeys = server.Keys(pattern: "token:*").ToList();
            var count = 0;
            
            foreach (var key in tokenKeys)
            {
                await db.KeyDeleteAsync(key);
                count++;
            }
            
            var result = new
            {
                DeletedCount = count,
                Message = $"Đã xóa {count} sessions khỏi Redis"
            };
            
            _logger.LogWarning("Cleared {Count} sessions from Redis", count);
            return Ok(ApiResponse<object>.Success(result, "Sessions cleared"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing Redis sessions");
            return StatusCode(500, ApiResponse<object>.Fail($"Error: {ex.Message}", 500));
        }
    }

    /// <summary>
    /// Trạng thái tổng quan hệ thống
    /// </summary>
    [HttpGet("status")]
    public ActionResult<ApiResponse<object>> GetStatus()
    {
        var status = new
        {
            API = "Running ✓",
            Timestamp = DateTime.UtcNow,
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
            Redis = new
            {
                IsConnected = _redis.IsConnected,
                Status = _redis.IsConnected ? "Connected ✓" : "Disconnected ✗",
                Endpoints = _redis.IsConnected ? _redis.GetEndPoints().Select(e => e.ToString()).ToArray() : Array.Empty<string>()
            }
        };

        return Ok(ApiResponse<object>.Success(status, "System is running"));
    }
}
