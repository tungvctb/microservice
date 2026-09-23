namespace BuildingBlocks.Contracts.IntegrationEvents;

/// <summary>Lệnh gửi thông báo — Notification service là consumer duy nhất của topic này.</summary>
public sealed record NotificationRequestedIntegrationEvent : IntegrationEvent
{
    public override string EventType => "notification.requested";
    public override string AggregateId => RecipientId?.ToString() ?? "broadcast";

    public Guid? RecipientId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string Severity { get; init; } = "Info";
    public string? Link { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}
