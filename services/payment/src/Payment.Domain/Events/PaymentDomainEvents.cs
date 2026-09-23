using BuildingBlocks.Core.Domain;

namespace Payment.Domain.Events;

public sealed record PaymentSucceededDomainEvent(
    Guid PaymentId, Guid OrderId, Guid CustomerId, decimal Amount, string Currency, string TransactionRef)
    : DomainEvent;

public sealed record PaymentFailedDomainEvent(
    Guid PaymentId, Guid OrderId, Guid CustomerId, decimal Amount, string Reason) : DomainEvent;

public sealed record PaymentRefundedDomainEvent(
    Guid PaymentId, Guid OrderId, decimal Amount, string Reason) : DomainEvent;
