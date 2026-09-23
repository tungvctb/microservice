using Product.Domain.Entities;

namespace Product.Application.Products;

internal static class Mapping
{
    public static ProductDto ToDto(this ProductItem p) => new(
        p.Id, p.Sku, p.Name, p.Description, p.Price.Amount, p.Price.Currency,
        p.CategoryId, p.Category?.Name, p.IsActive, p.ImageUrl, p.CreatedAtUtc, p.UpdatedAtUtc);

    public static ProductSummaryDto ToSummary(this ProductItem p) => new(
        p.Id, p.Sku, p.Name, p.Price.Amount, p.Price.Currency, p.Category?.Name, p.IsActive, p.ImageUrl);
}
