using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace EduVi.WebAPI.Hubs;

/// <summary>
/// SignalR Hub cho pipeline progress.
/// Client kết nối để nhận cập nhật real-time từ Python worker.
/// </summary>
[Authorize]
public class PipelineHub : Hub
{
    private readonly ILogger<PipelineHub> _logger;

    public PipelineHub(ILogger<PipelineHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Client join group theo userId từ JWT claims để nhận progress riêng.
    /// UserId được lấy từ token đã xác thực — client không thể tự khai báo.
    /// </summary>
    public async Task JoinUserGroup()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            throw new HubException("Unauthorized: cannot determine user identity.");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        _logger.LogInformation("User {UserId} joined SignalR group", userId);
    }

    /// <summary>
    /// Client rời group
    /// </summary>
    public async Task LeaveUserGroup()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            throw new HubException("Unauthorized: cannot determine user identity.");

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        _logger.LogInformation("User {UserId} left SignalR group", userId);
    }

    /// <summary>
    /// Fired when a client disconnects — clean shutdown, tab close, network drop, or logout.
    /// SignalR automatically evicts the ConnectionId from all groups, so no manual
    /// RemoveFromGroupAsync is needed here. This override exists purely for logging.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (exception is null)
            _logger.LogInformation("User {UserId} disconnected from SignalR (clean)", userId);
        else
            _logger.LogWarning(exception, "User {UserId} disconnected from SignalR (error)", userId);

        await base.OnDisconnectedAsync(exception);
    }
}
