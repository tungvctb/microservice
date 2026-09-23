namespace BuildingBlocks.Contracts.IntegrationEvents;

public sealed record ProductCreatedIntegrationEvent : IntegrationEvent
{
    public override string EventType => "product.created";
    public override string AggregateId => ProductId.ToString();

    public Guid ProductId { get; init; }
    public string Sku { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string Currency { get; init; } = "VND";
    public Guid CategoryId { get; init; }
    public int InitialStock { get; init; }
}

public sealed record ProductUpdatedIntegrationEvent : IntegrationEvent
{
    public override string EventType => "product.updated";
    public override string AggregateId => ProductId.ToString();

    public Guid ProductId { get; init; }
    public string Sku { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public sealed record ProductPriceChangedIntegrationEvent : IntegrationEvent
{
    public override string EventType => "product.price_changed";
    public override string AggregateId => ProductId.ToString();

    public Guid ProductId { get; init; }
    public string Sku { get; init; } = string.Empty;
    public decimal OldPrice { get; init; }
    public decimal NewPrice { get; init; }
    public string Currency { get; init; } = "VND";
}

public sealed record ProductDeletedIntegrationEvent : IntegrationEvent
{
    public override string EventType => "product.deleted";
    public override string AggregateId => ProductId.ToString();

    public Guid ProductId { get; init; }
    public string Sku { get; init; } = string.Empty;
}
