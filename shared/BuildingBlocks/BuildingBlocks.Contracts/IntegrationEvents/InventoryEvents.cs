namespace BuildingBlocks.Contracts.IntegrationEvents;

public sealed record StockReservedIntegrationEvent : IntegrationEvent
{
    public override string EventType => "inventory.stock_reserved";
    public override string AggregateId => OrderId.ToString();

    public Guid OrderId { get; init; }
    public Guid ReservationId { get; init; }
    public IReadOnlyList<ReservedLine> Lines { get; init; } = Array.Empty<ReservedLine>();
}

public sealed record ReservedLine(Guid ProductId, int Quantity);

public sealed record StockReservationFailedIntegrationEvent : IntegrationEvent
{
    public override string EventType => "inventory.reservation_failed";
    public override string AggregateId => OrderId.ToString();

    public Guid OrderId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public IReadOnlyList<InsufficientLine> Insufficient { get; init; } = Array.Empty<InsufficientLine>();
}

public sealed record InsufficientLine(Guid ProductId, int Requested, int Available);

public sealed record StockReleasedIntegrationEvent : IntegrationEvent
{
    public override string EventType => "inventory.stock_released";
    public override string AggregateId => OrderId.ToString();

    public Guid OrderId { get; init; }
    public Guid ReservationId { get; init; }
}

public sealed record StockLevelChangedIntegrationEvent : IntegrationEvent
{
    public override string EventType => "inventory.level_changed";
    public override string AggregateId => ProductId.ToString();

    public Guid ProductId { get; init; }
    public string Sku { get; init; } = string.Empty;
    public int QuantityOnHand { get; init; }
    public int QuantityAvailable { get; init; }
}

public sealed record LowStockDetectedIntegrationEvent : IntegrationEvent
{
    public override string EventType => "inventory.low_stock";
    public override string AggregateId => ProductId.ToString();

    public Guid ProductId { get; init; }
    public string Sku { get; init; } = string.Empty;
    public int QuantityAvailable { get; init; }
    public int ReorderLevel { get; init; }
}
