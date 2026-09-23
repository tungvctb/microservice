using BuildingBlocks.Core.Pagination;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;
using Notification.Domain.Repositories;

namespace Notification.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository(NotificationDbContext db) : INotificationRepository
{
    public Task<NotificationMessage?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<PagedResult<NotificationMessage>> SearchAsync(NotificationFilter filter,
        CancellationToken ct = default)
    {
        var query = db.Notifications.AsNoTracking().AsQueryable();

        if (filter.RecipientId.HasValue)
        {
            // Người dùng thấy thông báo của mình + thông báo broadcast (recipient = null).
            query = filter.IncludeBroadcast
                ? query.Where(n => n.RecipientId == filter.RecipientId || n.RecipientId == null)
                : query.Where(n => n.RecipientId == filter.RecipientId);
        }

        query = query
            .WhereIf(filter.IsRead.HasValue, n => n.IsRead == filter.IsRead!.Value)
            .WhereIf(filter.Severity.HasValue, n => n.Severity == filter.Severity!.Value);

        return await query.OrderByDescending(n => n.CreatedAtUtc).ToPagedResultAsync(filter, ct);
    }

    public Task<int> CountUnreadAsync(Guid recipientId, CancellationToken ct = default) =>
        db.Notifications.AsNoTracking()
            .CountAsync(n => !n.IsRead && (n.RecipientId == recipientId || n.RecipientId == null), ct);

    /// <summary>Cập nhật hàng loạt ngay tại DB — không nạp entity lên bộ nhớ.</summary>
    public Task<int> MarkAllAsReadAsync(Guid recipientId, CancellationToken ct = default) =>
        db.Notifications
            .Where(n => n.RecipientId == recipientId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAtUtc, DateTime.UtcNow), ct);

    public void Add(NotificationMessage notification) => db.Notifications.Add(notification);
    public void Remove(NotificationMessage notification) => db.Notifications.Remove(notification);
}
