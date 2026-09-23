using BuildingBlocks.Core.Pagination;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Product.Domain.Entities;
using Product.Domain.Repositories;

namespace Product.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository(ProductDbContext db) : IProductRepository
{
    public Task<ProductItem?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<ProductItem?> GetBySkuAsync(string sku, CancellationToken ct = default) =>
        db.Products.Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Sku == sku.ToUpperInvariant(), ct);

    public async Task<IReadOnlyList<ProductItem>> GetByIdsAsync(IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default) =>
        await db.Products.AsNoTracking().Include(p => p.Category)
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(ct);

    public async Task<PagedResult<ProductItem>> SearchAsync(ProductFilter filter, CancellationToken ct = default)
    {
        var query = db.Products.AsNoTracking().Include(p => p.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = $"%{filter.Search.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, term) || EF.Functions.ILike(p.Sku, term));
        }

        query = query
            .WhereIf(filter.CategoryId.HasValue, p => p.CategoryId == filter.CategoryId!.Value)
            .WhereIf(filter.IsActive.HasValue, p => p.IsActive == filter.IsActive!.Value)
            .WhereIf(filter.MinPrice.HasValue, p => p.Price.Amount >= filter.MinPrice!.Value)
            .WhereIf(filter.MaxPrice.HasValue, p => p.Price.Amount <= filter.MaxPrice!.Value);

        query = (filter.SortBy, filter.Descending) switch
        {
            ("name", true) => query.OrderByDescending(p => p.Name),
            ("name", false) => query.OrderBy(p => p.Name),
            ("price", true) => query.OrderByDescending(p => p.Price.Amount),
            ("price", false) => query.OrderBy(p => p.Price.Amount),
            ("sku", true) => query.OrderByDescending(p => p.Sku),
            ("sku", false) => query.OrderBy(p => p.Sku),
            (_, false) => query.OrderBy(p => p.CreatedAtUtc),
            _ => query.OrderByDescending(p => p.CreatedAtUtc)
        };

        return await query.ToPagedResultAsync(filter, ct);
    }

    public Task<bool> SkuExistsAsync(string sku, Guid? excludeId = null, CancellationToken ct = default) =>
        db.Products.AsNoTracking()
            .AnyAsync(p => p.Sku == sku.ToUpperInvariant() && (excludeId == null || p.Id != excludeId), ct);

    public void Add(ProductItem product) => db.Products.Add(product);
    public void Remove(ProductItem product) => db.Products.Remove(product);
}
