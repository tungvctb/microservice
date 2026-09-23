using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Notification.Infrastructure.Realtime;

/// <summary>
/// Hub SignalR cho thông báo realtime.
/// Mỗi client vào 2 nhóm: "user:{id}" (thông báo riêng) và "role:{role}" (cảnh báo vận hành).
/// Nhờ Redis backplane, client nối vào instance nào cũng nhận được message từ instance khác.
/// </summary>
[Authorize]
public sealed class NotificationHub(ILogger<NotificationHub> logger) : Hub
{
    public const string Path = "/hubs/notifications";

    public static string UserGroup(Guid userId) => $"user:{userId}";
    public static string RoleGroup(string role) => $"role:{role}";

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId is null)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId.Value));

        foreach (var role in Context.User!.FindAll(ClaimTypes.Role).Select(c => c.Value))
            await Groups.AddToGroupAsync(Context.ConnectionId, RoleGroup(role));

        logger.LogInformation("Client {ConnectionId} của user {UserId} đã kết nối",
            Context.ConnectionId, userId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("Client {ConnectionId} ngắt kết nối", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Client gọi để xác nhận kết nối còn sống (ngoài keep-alive mặc định).</summary>
    public Task<string> Ping() => Task.FromResult("pong");

    private Guid? GetUserId()
    {
        var raw = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? Context.User?.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
