using BuildingBlocks.Contracts.IntegrationEvents;

namespace BuildingBlocks.Infrastructure.Kafka;

/// <summary>Publish integration event lên Kafka.</summary>
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, string topic, CancellationToken ct = default)
        where TEvent : IntegrationEvent;

    /// <summary>Publish payload đã serialize sẵn (dùng bởi Outbox processor).</summary>
    Task PublishRawAsync(string topic, string key, string payload,
        IDictionary<string, string>? headers = null, CancellationToken ct = default);
}
