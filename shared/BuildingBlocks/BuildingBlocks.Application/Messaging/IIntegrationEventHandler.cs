using BuildingBlocks.Contracts.IntegrationEvents;

namespace BuildingBlocks.Application.Messaging;

/// <summary>
/// Xử lý 1 integration event nhận từ broker. Nằm ở tầng Application vì đây là use case,
/// không phải chi tiết hạ tầng — tầng Infrastructure chỉ lo việc lấy message từ Kafka rồi gọi vào đây.
/// </summary>
public interface IIntegrationEventHandler<in TEvent> where TEvent : IntegrationEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct);
}
