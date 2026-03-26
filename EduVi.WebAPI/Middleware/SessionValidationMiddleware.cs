using EduVi.Services.Authentication;
using System.Security.Claims;

namespace EduVi.WebAPI.Middleware;

/// <summary>
/// Middleware xác thực phiên làm việc với Redis
/// Đảm bảo Token trong Header khớp với Token lưu trong Redis
/// </summary>
public class SessionValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SessionValidationMiddleware> _logger;

    public SessionValidationMiddleware(RequestDelegate next, ILogger<SessionValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuthenticationService authService)
    {
        // Bỏ qua các endpoint public (login, register, etc.)
        var path = context.Request.Path.Value?.ToLower() ?? "";
        if (IsPublicEndpoint(path))
        {
            await _next(context);
            return;
        }

        // Chỉ validate nếu user đã authenticated
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var userId))
            {
                var token = GetTokenFromHeader(context);
                if (!string.IsNullOrEmpty(token))
                {
                    var isValid = await authService.VerifySessionAsync(userId, token);
                    if (!isValid)
                    {
                        _logger.LogWarning("Invalid session for user {UserId}", userId);
                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            Code = 401,
                            Message = "Session expired or invalid. Please login again.",
                            Result = (object?)null
                        });
                        return;
                    }
                }
            }
        }

        await _next(context);
    }

    private bool IsPublicEndpoint(string path)
    {
        var publicEndpoints = new[]
        {
            "/api/auth/login",
            "/api/auth/register",
            "/api/auth/google-login",
            "/api/auth/forgot-password",
            "/api/auth/reset-password",
            "/swagger"
        };

        return publicEndpoints.Any(endpoint => path.StartsWith(endpoint));
    }

    private string? GetTokenFromHeader(HttpContext context)
    {
        // Standard REST requests: Authorization: Bearer <token>
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            return authHeader.Substring("Bearer ".Length).Trim();

        // SignalR (WebSocket / SSE): token arrives as ?access_token= because
        // browsers cannot set Authorization headers on WebSocket connections.
        // Program.cs already reads it in JwtBearerEvents.OnMessageReceived,
        // so we mirror that logic here to enforce session validation for hubs too.
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/hubs", StringComparison.OrdinalIgnoreCase))
        {
            var queryToken = context.Request.Query["access_token"].FirstOrDefault();
            if (!string.IsNullOrEmpty(queryToken))
                return queryToken;
        }

        return null;
    }
}

/// <summary>
/// Extension method để đăng ký middleware
/// </summary>
public static class SessionValidationMiddlewareExtensions
{
    public static IApplicationBuilder UseSessionValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SessionValidationMiddleware>();
    }
}
