using BuildingBlocks.Core.Domain;

namespace BuildingBlocks.Application.Cqrs;

/// <summary>Xử lý domain event in-process, chạy ngay sau khi SaveChanges thành công.</summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct);
}
