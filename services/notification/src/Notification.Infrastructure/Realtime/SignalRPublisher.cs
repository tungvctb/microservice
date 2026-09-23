using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Notification.Application.Abstractions;
using Notification.Application.Common;

namespace Notification.Infrastructure.Realtime;

public sealed class SignalRPublisher(
    IHubContext<NotificationHub> hub,
    ILogger<SignalRPublisher> logger) : IRealtimePublisher
{
    /// <summary>Tên method phía client — FE đăng ký bằng connection.on("ReceiveNotification", ...).</summary>
    private const string ReceiveNotification = "ReceiveNotification";
    private const string UnreadCountChanged = "UnreadCountChanged";

    public async Task PushToUserAsync(Guid userId, NotificationDto notification, CancellationToken ct = default)
    {
        await hub.Clients.Group(NotificationHub.UserGroup(userId))
            .SendAsync(ReceiveNotification, notification, ct);

        logger.LogDebug("Đã đẩy thông báo {Id} tới user {UserId}", notification.Id, userId);
    }

    public Task PushToRoleAsync(string role, NotificationDto notification, CancellationToken ct = default) =>
        hub.Clients.Group(NotificationHub.RoleGroup(role))
            .SendAsync(ReceiveNotification, notification, ct);

    public Task BroadcastAsync(NotificationDto notification, CancellationToken ct = default) =>
        hub.Clients.All.SendAsync(ReceiveNotification, notification, ct);

    public Task PushUnreadCountAsync(Guid userId, int unreadCount, CancellationToken ct = default) =>
        hub.Clients.Group(NotificationHub.UserGroup(userId))
            .SendAsync(UnreadCountChanged, unreadCount, ct);
}
