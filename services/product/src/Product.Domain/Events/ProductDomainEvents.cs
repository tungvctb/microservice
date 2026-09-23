using BuildingBlocks.Core.Domain;

namespace Product.Domain.Events;

public sealed record ProductCreatedDomainEvent(
    Guid ProductId, string Sku, string Name, decimal Price, string Currency,
    Guid CategoryId, int InitialStock) : DomainEvent;

public sealed record ProductUpdatedDomainEvent(
    Guid ProductId, string Sku, string Name, bool IsActive) : DomainEvent;

public sealed record ProductPriceChangedDomainEvent(
    Guid ProductId, string Sku, decimal OldPrice, decimal NewPrice, string Currency) : DomainEvent;

public sealed record ProductDeletedDomainEvent(Guid ProductId, string Sku) : DomainEvent;
