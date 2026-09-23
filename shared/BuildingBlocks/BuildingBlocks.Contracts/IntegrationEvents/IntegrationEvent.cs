namespace BuildingBlocks.Contracts.IntegrationEvents;

/// <summary>
/// Hợp đồng sự kiện giữa các service. Serialize JSON, publish lên Kafka.
/// Key của message = <see cref="AggregateId"/> để mọi sự kiện của cùng 1 aggregate
/// rơi vào cùng partition, giữ đúng thứ tự.
/// </summary>
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
    public string? CorrelationId { get; init; }

    /// <summary>Tên định danh dùng để route tới handler ở phía consumer.</summary>
    public abstract string EventType { get; }

    /// <summary>Dùng làm Kafka message key.</summary>
    public abstract string AggregateId { get; }
}
