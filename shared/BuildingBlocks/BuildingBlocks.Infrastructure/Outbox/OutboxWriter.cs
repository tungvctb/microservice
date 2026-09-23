using System.Text.Json;
using BuildingBlocks.Application.Messaging;
using BuildingBlocks.Application.Security;
using BuildingBlocks.Contracts.IntegrationEvents;

namespace BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Đẩy event vào bảng Outbox (chưa gọi SaveChanges — để nằm chung transaction với thay đổi nghiệp vụ).
/// </summary>
public sealed class OutboxWriter(IIntegrationDbContext db, ICurrentUser? currentUser = null)
    : IOutboxWriter, IIntegrationEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Enqueue<TEvent>(TEvent @event) where TEvent : class
    {
        if (@event is not IntegrationEvent integrationEvent)
            throw new ArgumentException($"{typeof(TEvent).Name} phải kế thừa IntegrationEvent.", nameof(@event));

        db.OutboxMessages.Add(new OutboxMessage
        {
            EventId = integrationEvent.EventId,
            EventType = integrationEvent.EventType,
            Topic = TopicResolver.For(integrationEvent),
            AggregateId = integrationEvent.AggregateId,
            Payload = JsonSerializer.Serialize(@event, @event.GetType(), JsonOptions),
            CorrelationId = integrationEvent.CorrelationId ?? currentUser?.CorrelationId,
            OccurredOnUtc = integrationEvent.OccurredOnUtc
        });
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class
    {
        Enqueue(@event);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Quy ước: event thuộc context nào thì vào topic của context đó.</summary>
public static class TopicResolver
{
    public static string For(IntegrationEvent @event) => @event.EventType.Split('.')[0] switch
    {
        "product" => KafkaTopics.ProductEvents,
        "inventory" => KafkaTopics.InventoryEvents,
        "order" => KafkaTopics.OrderEvents,
        "payment" => KafkaTopics.PaymentEvents,
        "notification" => KafkaTopics.NotificationCommands,
        _ => throw new InvalidOperationException($"Không xác định được topic cho '{@event.EventType}'.")
    };
}
