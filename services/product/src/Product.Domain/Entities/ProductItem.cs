using BuildingBlocks.Core.Domain;
using BuildingBlocks.Core.Exceptions;
using Product.Domain.Events;

namespace Product.Domain.Entities;

/// <summary>
/// Aggregate root của catalog. Đặt tên ProductItem để không đụng namespace gốc "Product".
/// Tồn kho KHÔNG nằm ở đây — đó là trách nhiệm của Inventory service.
/// </summary>
public sealed class ProductItem : AggregateRoot
{
    private ProductItem() { }

    public string Sku { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Money Price { get; private set; } = Money.Zero();
    public Guid CategoryId { get; private set; }
    public Category? Category { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? ImageUrl { get; private set; }

    public static ProductItem Create(string sku, string name, string? description,
        decimal price, string currency, Guid categoryId, string? imageUrl, int initialStock = 0)
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new DomainException("product.sku_required", "SKU không được rỗng.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("product.name_required", "Tên sản phẩm không được rỗng.");

        var product = new ProductItem
        {
            Sku = sku.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Description = description?.Trim(),
            Price = Money.Of(price, currency),
            CategoryId = categoryId,
            ImageUrl = imageUrl
        };

        product.Raise(new ProductCreatedDomainEvent(product.Id, product.Sku, product.Name,
            product.Price.Amount, product.Price.Currency, categoryId, initialStock));

        return product;
    }

    public void UpdateDetails(string name, string? description, Guid categoryId, string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("product.name_required", "Tên sản phẩm không được rỗng.");

        Name = name.Trim();
        Description = description?.Trim();
        CategoryId = categoryId;
        ImageUrl = imageUrl;
        Touch();

        Raise(new ProductUpdatedDomainEvent(Id, Sku, Name, IsActive));
    }

    /// <summary>Đổi giá phát event riêng vì Order/Inventory quan tâm tới thay đổi này.</summary>
    public void ChangePrice(decimal newPrice, string currency)
    {
        var updated = Money.Of(newPrice, currency);
        if (updated.Equals(Price)) return;

        var oldPrice = Price;
        Price = updated;
        Touch();

        Raise(new ProductPriceChangedDomainEvent(Id, Sku, oldPrice.Amount, updated.Amount, updated.Currency));
    }

    public void SetActive(bool isActive)
    {
        if (IsActive == isActive) return;
        IsActive = isActive;
        Touch();
        Raise(new ProductUpdatedDomainEvent(Id, Sku, Name, IsActive));
    }

    public void MarkDeleted() => Raise(new ProductDeletedDomainEvent(Id, Sku));
}
