namespace BuildingBlocks.Contracts.IntegrationEvents;

public sealed record OrderPlacedIntegrationEvent : IntegrationEvent
{
    public override string EventType => "order.placed";
    public override string AggregateId => OrderId.ToString();

    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string CustomerEmail { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = "VND";
    public string PaymentMethod { get; init; } = "CreditCard";
    public IReadOnlyList<OrderLineDto> Lines { get; init; } = Array.Empty<OrderLineDto>();
}

public sealed record OrderLineDto(Guid ProductId, string Sku, string Name, int Quantity, decimal UnitPrice);

public sealed record OrderConfirmedIntegrationEvent : IntegrationEvent
{
    public override string EventType => "order.confirmed";
    public override string AggregateId => OrderId.ToString();

    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public decimal TotalAmount { get; init; }
}

public sealed record OrderCancelledIntegrationEvent : IntegrationEvent
{
    public override string EventType => "order.cancelled";
    public override string AggregateId => OrderId.ToString();

    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public Guid? ReservationId { get; init; }
}

public sealed record OrderCompletedIntegrationEvent : IntegrationEvent
{
    public override string EventType => "order.completed";
    public override string AggregateId => OrderId.ToString();

    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
}
