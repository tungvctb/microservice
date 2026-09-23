using BuildingBlocks.Core.Domain;

namespace Order.Domain.Events;

public sealed record OrderPlacedDomainEvent(
    Guid OrderId, string OrderNumber, Guid CustomerId, string CustomerEmail,
    decimal TotalAmount, string Currency, string PaymentMethod,
    IReadOnlyList<(Guid ProductId, string Sku, string Name, int Quantity, decimal UnitPrice)> Lines)
    : DomainEvent;

public sealed record OrderConfirmedDomainEvent(
    Guid OrderId, string OrderNumber, Guid CustomerId, decimal TotalAmount) : DomainEvent;

public sealed record OrderCancelledDomainEvent(
    Guid OrderId, string OrderNumber, Guid CustomerId, string Reason, Guid? ReservationId) : DomainEvent;

public sealed record OrderCompletedDomainEvent(
    Guid OrderId, string OrderNumber, Guid CustomerId) : DomainEvent;
