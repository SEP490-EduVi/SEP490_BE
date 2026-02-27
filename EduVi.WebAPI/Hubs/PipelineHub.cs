using Microsoft.AspNetCore.SignalR;

namespace EduVi.WebAPI.Hubs;

/// <summary>
/// SignalR Hub cho pipeline progress.
/// Client kết nối để nhận cập nhật real-time từ Python worker.
/// Tất cả pushing được thực hiện từ BackgroundService qua IHubContext, không từ Hub.
/// </summary>
public class PipelineHub : Hub
{
    /// <summary>
    /// Client join group theo userId để nhận progress riêng
    /// </summary>
    public async Task JoinUserGroup(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
    }

    /// <summary>
    /// Client rời group
    /// </summary>
    public async Task LeaveUserGroup(string userId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
    }
}
