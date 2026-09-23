using BuildingBlocks.Core.Pagination;
using Product.Domain.Entities;

namespace Product.Domain.Repositories;

public sealed record ProductFilter : PageRequest
{
    public string? Search { get; init; }
    public Guid? CategoryId { get; init; }
    public bool? IsActive { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public string SortBy { get; init; } = "createdAt";
    public bool Descending { get; init; } = true;
}

public interface IProductRepository
{
    Task<ProductItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProductItem?> GetBySkuAsync(string sku, CancellationToken ct = default);
    Task<IReadOnlyList<ProductItem>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<PagedResult<ProductItem>> SearchAsync(ProductFilter filter, CancellationToken ct = default);
    Task<bool> SkuExistsAsync(string sku, Guid? excludeId = null, CancellationToken ct = default);
    void Add(ProductItem product);
    void Remove(ProductItem product);
}

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Category>> GetAllAsync(bool onlyActive, CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default);
    Task<bool> HasProductsAsync(Guid categoryId, CancellationToken ct = default);
    void Add(Category category);
    void Remove(Category category);
}
