using Notification.Domain.Entities;
using Notification.Domain.Enums;

namespace Notification.Application.Common;

public sealed record NotificationDto(
    Guid Id, Guid? RecipientId, string Title, string Body,
    NotificationSeverity Severity, NotificationChannel Channel,
    string? Link, IReadOnlyDictionary<string, string> Metadata,
    bool IsRead, DateTime? ReadAtUtc, DateTime CreatedAtUtc);

public static class NotificationMapping
{
    public static NotificationDto ToDto(this NotificationMessage n) => new(
        n.Id, n.RecipientId, n.Title, n.Body, n.Severity, n.Channel,
        n.Link, n.Metadata, n.IsRead, n.ReadAtUtc, n.CreatedAtUtc);
}
