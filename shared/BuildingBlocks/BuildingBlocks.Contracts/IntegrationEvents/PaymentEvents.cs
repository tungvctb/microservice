namespace BuildingBlocks.Contracts.IntegrationEvents;

public sealed record PaymentSucceededIntegrationEvent : IntegrationEvent
{
    public override string EventType => "payment.succeeded";
    public override string AggregateId => OrderId.ToString();

    public Guid PaymentId { get; init; }
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "VND";
    public string TransactionRef { get; init; } = string.Empty;
}

public sealed record PaymentFailedIntegrationEvent : IntegrationEvent
{
    public override string EventType => "payment.failed";
    public override string AggregateId => OrderId.ToString();

    public Guid PaymentId { get; init; }
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public decimal Amount { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed record PaymentRefundedIntegrationEvent : IntegrationEvent
{
    public override string EventType => "payment.refunded";
    public override string AggregateId => OrderId.ToString();

    public Guid PaymentId { get; init; }
    public Guid OrderId { get; init; }
    public decimal Amount { get; init; }
    public string Reason { get; init; } = string.Empty;
}
