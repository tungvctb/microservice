namespace BuildingBlocks.Core.Domain;

/// <summary>Sự kiện phát sinh bên trong 1 aggregate, xử lý in-process sau khi SaveChanges.</summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredOnUtc { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
