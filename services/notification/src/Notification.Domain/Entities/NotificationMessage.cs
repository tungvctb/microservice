using BuildingBlocks.Core.Domain;
using BuildingBlocks.Core.Exceptions;
using Notification.Domain.Enums;

namespace Notification.Domain.Entities;

/// <summary>
/// Thông báo gửi tới 1 người dùng, hoặc broadcast khi RecipientId = null.
/// Lưu DB để người dùng đọc lại được lịch sử, không chỉ tồn tại trong lúc online.
/// </summary>
public sealed class NotificationMessage : AggregateRoot
{
    private NotificationMessage() { }

    public Guid? RecipientId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public NotificationSeverity Severity { get; private set; } = NotificationSeverity.Info;
    public NotificationChannel Channel { get; private set; } = NotificationChannel.InApp;
    public string? Link { get; private set; }
    public Dictionary<string, string> Metadata { get; private set; } = new();
    public bool IsRead { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }

    public bool IsBroadcast => RecipientId is null;

    public static NotificationMessage Create(Guid? recipientId, string title, string body,
        NotificationSeverity severity = NotificationSeverity.Info,
        string? link = null, Dictionary<string, string>? metadata = null,
        NotificationChannel channel = NotificationChannel.InApp)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("notification.title_required", "Tiêu đề không được rỗng.");

        return new NotificationMessage
        {
            RecipientId = recipientId,
            Title = title.Trim(),
            Body = body.Trim(),
            Severity = severity,
            Link = link,
            Metadata = metadata ?? new Dictionary<string, string>(),
            Channel = channel
        };
    }

    public void MarkAsRead()
    {
        if (IsRead) return;
        IsRead = true;
        ReadAtUtc = DateTime.UtcNow;
        Touch();
    }
}
