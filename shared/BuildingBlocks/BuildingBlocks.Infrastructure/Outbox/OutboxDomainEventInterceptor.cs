using System.Text.Json;
using BuildingBlocks.Application.Security;
using BuildingBlocks.Contracts.IntegrationEvents;
using BuildingBlocks.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Outbox;

/// <summary>Mỗi service khai báo cách quy đổi domain event nội bộ sang integration event công khai.</summary>
public interface IIntegrationEventMapper
{
    IntegrationEvent? Map(IDomainEvent domainEvent);
}

/// <summary>
/// Chạy TRƯỚC SaveChanges: chuyển domain event thành bản ghi outbox nằm chung transaction
/// với dữ liệu nghiệp vụ. Nhờ vậy không bao giờ có chuyện "ghi DB xong mà event bốc hơi".
/// Domain event KHÔNG bị clear ở đây — DomainEventDispatchInterceptor vẫn dùng được sau khi commit.
/// </summary>
public sealed class OutboxDomainEventInterceptor(
    IIntegrationEventMapper mapper,
    ILogger<OutboxDomainEventInterceptor> logger,
    ICurrentUser? currentUser = null) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) WriteOutbox(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null) WriteOutbox(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void WriteOutbox(DbContext context)
    {
        var domainEvents = context.ChangeTracker.Entries<AggregateRoot>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        if (domainEvents.Count == 0) return;

        var correlationId = currentUser?.CorrelationId;

        foreach (var domainEvent in domainEvents)
        {
            var integrationEvent = mapper.Map(domainEvent);
            if (integrationEvent is null) continue;   // event chỉ dùng nội bộ

            integrationEvent = integrationEvent with { CorrelationId = correlationId };

            context.Set<OutboxMessage>().Add(new OutboxMessage
            {
                EventId = integrationEvent.EventId,
                EventType = integrationEvent.EventType,
                Topic = TopicResolver.For(integrationEvent),
                AggregateId = integrationEvent.AggregateId,
                Payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), JsonOptions),
                CorrelationId = correlationId,
                OccurredOnUtc = integrationEvent.OccurredOnUtc
            });

            logger.LogDebug("Ghi outbox {EventType} cho aggregate {AggregateId}",
                integrationEvent.EventType, integrationEvent.AggregateId);
        }
    }
}
