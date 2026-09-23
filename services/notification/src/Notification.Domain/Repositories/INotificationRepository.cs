using BuildingBlocks.Core.Pagination;
using Notification.Domain.Entities;
using Notification.Domain.Enums;

namespace Notification.Domain.Repositories;

public sealed record NotificationFilter : PageRequest
{
    public Guid? RecipientId { get; init; }
    public bool? IsRead { get; init; }
    public NotificationSeverity? Severity { get; init; }
    public bool IncludeBroadcast { get; init; } = true;
}

public interface INotificationRepository
{
    Task<NotificationMessage?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<NotificationMessage>> SearchAsync(NotificationFilter filter, CancellationToken ct = default);
    Task<int> CountUnreadAsync(Guid recipientId, CancellationToken ct = default);
    Task<int> MarkAllAsReadAsync(Guid recipientId, CancellationToken ct = default);
    void Add(NotificationMessage notification);
    void Remove(NotificationMessage notification);
}
