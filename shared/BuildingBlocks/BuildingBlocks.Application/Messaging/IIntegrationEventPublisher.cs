namespace BuildingBlocks.Application.Messaging;

/// <summary>Phát integration event ra message broker (qua Outbox để đảm bảo atomic với DB).</summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class;
}

/// <summary>Ghi event vào Outbox trong cùng transaction với thay đổi dữ liệu.</summary>
public interface IOutboxWriter
{
    void Enqueue<TEvent>(TEvent @event) where TEvent : class;
}
