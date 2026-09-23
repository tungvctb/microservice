using Notification.Application.Common;

namespace Notification.Application.Abstractions;

/// <summary>
/// Đẩy thông báo realtime tới client. Application chỉ biết "gửi cho ai, nội dung gì";
/// SignalR + Redis backplane là chi tiết của Infrastructure.
/// </summary>
public interface IRealtimePublisher
{
    Task PushToUserAsync(Guid userId, NotificationDto notification, CancellationToken ct = default);
    Task PushToRoleAsync(string role, NotificationDto notification, CancellationToken ct = default);
    Task BroadcastAsync(NotificationDto notification, CancellationToken ct = default);
    Task PushUnreadCountAsync(Guid userId, int unreadCount, CancellationToken ct = default);
}
