using BuildingBlocks.Contracts.IntegrationEvents;
using BuildingBlocks.Core.Domain;
using BuildingBlocks.Infrastructure.Outbox;
using Product.Domain.Events;

namespace Product.Infrastructure.Messaging;

/// <summary>
/// Ranh giới giữa mô hình nội bộ và hợp đồng công khai: đổi tên/đổi shape domain event
/// không ảnh hưởng service khác chừng nào mapping này giữ nguyên.
/// </summary>
public sealed class ProductIntegrationEventMapper : IIntegrationEventMapper
{
    public IntegrationEvent? Map(IDomainEvent domainEvent) => domainEvent switch
    {
        ProductCreatedDomainEvent e => new ProductCreatedIntegrationEvent
        {
            ProductId = e.ProductId,
            Sku = e.Sku,
            Name = e.Name,
            Price = e.Price,
            Currency = e.Currency,
            CategoryId = e.CategoryId,
            InitialStock = e.InitialStock
        },
        ProductUpdatedDomainEvent e => new ProductUpdatedIntegrationEvent
        {
            ProductId = e.ProductId,
            Sku = e.Sku,
            Name = e.Name,
            IsActive = e.IsActive
        },
        ProductPriceChangedDomainEvent e => new ProductPriceChangedIntegrationEvent
        {
            ProductId = e.ProductId,
            Sku = e.Sku,
            OldPrice = e.OldPrice,
            NewPrice = e.NewPrice,
            Currency = e.Currency
        },
        ProductDeletedDomainEvent e => new ProductDeletedIntegrationEvent
        {
            ProductId = e.ProductId,
            Sku = e.Sku
        },
        _ => null
    };
}
