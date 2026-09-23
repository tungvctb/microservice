namespace Product.Application.Categories;

public sealed record CategoryDto(
    Guid Id, string Name, string Slug, string? Description, bool IsActive,
    int ProductCount, DateTime CreatedAtUtc);
