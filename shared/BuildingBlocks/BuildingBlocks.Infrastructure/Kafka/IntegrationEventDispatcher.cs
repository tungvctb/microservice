using BuildingBlocks.Application.Messaging;
using System.Collections.Concurrent;
using BuildingBlocks.Contracts.IntegrationEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Kafka;

/// <summary>Gọi tất cả IIntegrationEventHandler đăng ký cho 1 event đã deserialize.</summary>
public sealed class IntegrationEventDispatcher(ILogger<IntegrationEventDispatcher> logger)
{
    private static readonly ConcurrentDictionary<Type, HandlerInvoker> Invokers = new();

    public async Task<int> DispatchAsync(IntegrationEvent @event, IServiceProvider scope, CancellationToken ct)
    {
        var invoker = Invokers.GetOrAdd(@event.GetType(), static t =>
            (HandlerInvoker)Activator.CreateInstance(typeof(HandlerInvoker<>).MakeGenericType(t))!);

        var count = await invoker.InvokeAsync(@event, scope, ct);

        if (count == 0)
            logger.LogDebug("Không có handler cho {EventType} — bỏ qua", @event.EventType);

        return count;
    }

    private abstract class HandlerInvoker
    {
        public abstract Task<int> InvokeAsync(IntegrationEvent @event, IServiceProvider sp, CancellationToken ct);
    }

    private sealed class HandlerInvoker<TEvent> : HandlerInvoker where TEvent : IntegrationEvent
    {
        public override async Task<int> InvokeAsync(IntegrationEvent @event, IServiceProvider sp, CancellationToken ct)
        {
            var handlers = sp.GetServices<IIntegrationEventHandler<TEvent>>().ToArray();
            foreach (var handler in handlers)
                await handler.HandleAsync((TEvent)@event, ct);
            return handlers.Length;
        }
    }
}
