using BuildingBlocks.Core.Domain;

namespace Product.Domain.Entities;

public sealed class Category : AggregateRoot
{
    private readonly List<ProductItem> _products = new();

    private Category() { }   // cho EF Core

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<ProductItem> Products => _products.AsReadOnly();

    public static Category Create(string name, string? description = null)
    {
        var category = new Category
        {
            Name = name.Trim(),
            Slug = ToSlug(name),
            Description = description?.Trim()
        };
        return category;
    }

    public void Update(string name, string? description, bool isActive)
    {
        Name = name.Trim();
        Slug = ToSlug(name);
        Description = description?.Trim();
        IsActive = isActive;
        Touch();
    }

    private static string ToSlug(string value)
    {
        var normalized = value.Trim().ToLowerInvariant()
            .Replace("đ", "d")
            .Normalize(System.Text.NormalizationForm.FormD);

        var chars = normalized
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                        != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();

        return string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
    }
}
