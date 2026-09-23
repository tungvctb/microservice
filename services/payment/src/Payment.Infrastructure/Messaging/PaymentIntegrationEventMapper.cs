using BuildingBlocks.Contracts.IntegrationEvents;
using BuildingBlocks.Core.Domain;
using BuildingBlocks.Infrastructure.Outbox;
using Payment.Domain.Events;

namespace Payment.Infrastructure.Messaging;

public sealed class PaymentIntegrationEventMapper : IIntegrationEventMapper
{
    public IntegrationEvent? Map(IDomainEvent domainEvent) => domainEvent switch
    {
        PaymentSucceededDomainEvent e => new PaymentSucceededIntegrationEvent
        {
            PaymentId = e.PaymentId,
            OrderId = e.OrderId,
            CustomerId = e.CustomerId,
            Amount = e.Amount,
            Currency = e.Currency,
            TransactionRef = e.TransactionRef
        },
        PaymentFailedDomainEvent e => new PaymentFailedIntegrationEvent
        {
            PaymentId = e.PaymentId,
            OrderId = e.OrderId,
            CustomerId = e.CustomerId,
            Amount = e.Amount,
            Reason = e.Reason
        },
        PaymentRefundedDomainEvent e => new PaymentRefundedIntegrationEvent
        {
            PaymentId = e.PaymentId,
            OrderId = e.OrderId,
            Amount = e.Amount,
            Reason = e.Reason
        },
        _ => null
    };
}
