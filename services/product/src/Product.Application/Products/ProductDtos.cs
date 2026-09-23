namespace Product.Application.Products;

public sealed record ProductDto(
    Guid Id,
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    Guid CategoryId,
    string? CategoryName,
    bool IsActive,
    string? ImageUrl,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record ProductSummaryDto(
    Guid Id, string Sku, string Name, decimal Price, string Currency,
    string? CategoryName, bool IsActive, string? ImageUrl);
