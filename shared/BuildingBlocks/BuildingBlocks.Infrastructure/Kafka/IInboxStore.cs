namespace BuildingBlocks.Infrastructure.Kafka;

/// <summary>
/// Inbox pattern: ghi lại EventId đã xử lý để consumer chịu được at-least-once của Kafka.
/// Service nào cần "exactly-once về mặt hiệu ứng" thì đăng ký implementation dựa trên DB.
/// </summary>
public interface IInboxStore
{
    Task<bool> HasProcessedAsync(Guid eventId, string consumerName, CancellationToken ct);
    Task MarkProcessedAsync(Guid eventId, string consumerName, string eventType, CancellationToken ct);
}
