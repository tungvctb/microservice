namespace BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Transactional Outbox: event được ghi cùng transaction với dữ liệu nghiệp vụ,
/// sau đó 1 background job đẩy lên Kafka. Tránh cảnh "đã ghi DB nhưng mất event" (hoặc ngược lại).
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public DateTime OccurredOnUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedOnUtc { get; set; }
    public int RetryCount { get; set; }
    public string? Error { get; set; }
}

/// <summary>Bản ghi event đã consume — chặn xử lý lặp khi Kafka giao lại message.</summary>
public sealed class InboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public string ConsumerName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime ProcessedOnUtc { get; set; } = DateTime.UtcNow;
}
