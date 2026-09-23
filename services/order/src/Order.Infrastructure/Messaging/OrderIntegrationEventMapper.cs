using BuildingBlocks.Contracts.IntegrationEvents;
using BuildingBlocks.Core.Domain;
using BuildingBlocks.Infrastructure.Outbox;
using Order.Domain.Events;

namespace Order.Infrastructure.Messaging;

public sealed class OrderIntegrationEventMapper : IIntegrationEventMapper
{
    public IntegrationEvent? Map(IDomainEvent domainEvent) => domainEvent switch
    {
        OrderPlacedDomainEvent e => new OrderPlacedIntegrationEvent
        {
            OrderId = e.OrderId,
            OrderNumber = e.OrderNumber,
            CustomerId = e.CustomerId,
            CustomerEmail = e.CustomerEmail,
            TotalAmount = e.TotalAmount,
            Currency = e.Currency,
            PaymentMethod = e.PaymentMethod,
            Lines = e.Lines
                .Select(l => new OrderLineDto(l.ProductId, l.Sku, l.Name, l.Quantity, l.UnitPrice))
                .ToList()
        },
        OrderConfirmedDomainEvent e => new OrderConfirmedIntegrationEvent
        {
            OrderId = e.OrderId,
            OrderNumber = e.OrderNumber,
            CustomerId = e.CustomerId,
            TotalAmount = e.TotalAmount
        },
        OrderCancelledDomainEvent e => new OrderCancelledIntegrationEvent
        {
            OrderId = e.OrderId,
            OrderNumber = e.OrderNumber,
            CustomerId = e.CustomerId,
            Reason = e.Reason,
            ReservationId = e.ReservationId
        },
        OrderCompletedDomainEvent e => new OrderCompletedIntegrationEvent
        {
            OrderId = e.OrderId,
            OrderNumber = e.OrderNumber,
            CustomerId = e.CustomerId
        },
        _ => null
    };
}
