using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Sau khi SaveChanges thành công, gom domain event của mọi aggregate đang track và gọi handler.
/// Đặt sau SaveChanges (không phải trước) để handler chỉ chạy khi dữ liệu đã thực sự được ghi.
/// </summary>
public sealed class DomainEventDispatchInterceptor(
    IServiceScopeFactory scopeFactory,
    ILogger<DomainEventDispatchInterceptor> logger) : SaveChangesInterceptor
{
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context is null) return result;

        var aggregates = context.ChangeTracker.Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        if (aggregates.Count == 0) return result;

        var events = aggregates.SelectMany(a => a.DomainEvents).ToList();
        aggregates.ForEach(a => a.ClearDomainEvents());

        using var scope = scopeFactory.CreateScope();
        foreach (var domainEvent in events)
        {
            try
            {
                var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
                foreach (var handler in scope.ServiceProvider.GetServices(handlerType))
                {
                    if (handler is null) continue;
                    var method = handlerType.GetMethod("HandleAsync")!;
                    await (Task)method.Invoke(handler, new object[] { domainEvent, cancellationToken })!;
                }
            }
            catch (Exception ex)
            {
                // Domain event handler lỗi không được rollback dữ liệu đã commit.
                logger.LogError(ex, "Handler của domain event {Event} lỗi", domainEvent.GetType().Name);
            }
        }

        return result;
    }
}
