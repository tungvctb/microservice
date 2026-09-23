using BuildingBlocks.Core.Domain;

namespace Inventory.Domain.Events;

public sealed record StockLevelChangedDomainEvent(
    Guid ProductId, string Sku, int QuantityOnHand, int QuantityAvailable) : DomainEvent;

public sealed record LowStockDetectedDomainEvent(
    Guid ProductId, string Sku, int QuantityAvailable, int ReorderLevel) : DomainEvent;

public sealed record StockReservedDomainEvent(
    Guid OrderId, Guid ReservationId, IReadOnlyList<(Guid ProductId, int Quantity)> Lines) : DomainEvent;

public sealed record StockReservationFailedDomainEvent(
    Guid OrderId, string Reason, IReadOnlyList<(Guid ProductId, int Requested, int Available)> Insufficient)
    : DomainEvent;

public sealed record StockReleasedDomainEvent(Guid OrderId, Guid ReservationId) : DomainEvent;
