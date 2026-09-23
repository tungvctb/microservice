using BuildingBlocks.Contracts.IntegrationEvents;
using BuildingBlocks.Core.Domain;
using BuildingBlocks.Infrastructure.Outbox;
using Inventory.Domain.Events;

namespace Inventory.Infrastructure.Messaging;

public sealed class InventoryIntegrationEventMapper : IIntegrationEventMapper
{
    public IntegrationEvent? Map(IDomainEvent domainEvent) => domainEvent switch
    {
        StockLevelChangedDomainEvent e => new StockLevelChangedIntegrationEvent
        {
            ProductId = e.ProductId,
            Sku = e.Sku,
            QuantityOnHand = e.QuantityOnHand,
            QuantityAvailable = e.QuantityAvailable
        },
        LowStockDetectedDomainEvent e => new LowStockDetectedIntegrationEvent
        {
            ProductId = e.ProductId,
            Sku = e.Sku,
            QuantityAvailable = e.QuantityAvailable,
            ReorderLevel = e.ReorderLevel
        },
        StockReservedDomainEvent e => new StockReservedIntegrationEvent
        {
            OrderId = e.OrderId,
            ReservationId = e.ReservationId,
            Lines = e.Lines.Select(l => new ReservedLine(l.ProductId, l.Quantity)).ToList()
        },
        StockReleasedDomainEvent e => new StockReleasedIntegrationEvent
        {
            OrderId = e.OrderId,
            ReservationId = e.ReservationId
        },
        _ => null
    };
}
