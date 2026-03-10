using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace EduVi.WebAPI.Hubs;

/// <summary>
/// SignalR Hub cho pipeline progress.
/// Client kết nối để nhận cập nhật real-time từ Python worker.
/// Tất cả pushing được thực hiện từ BackgroundService qua IHubContext, không từ Hub.
/// </summary>
[Authorize]
public class PipelineHub : Hub
{
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
    }
}
