using Microsoft.EntityFrameworkCore;
using Product.Domain.Entities;
using Product.Domain.Repositories;

namespace Product.Infrastructure.Persistence.Repositories;

public sealed class CategoryRepository(ProductDbContext db) : ICategoryRepository
{
    public Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Categories.Include(c => c.Products).FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Category>> GetAllAsync(bool onlyActive, CancellationToken ct = default) =>
        await db.Categories.AsNoTracking().Include(c => c.Products)
            .Where(c => !onlyActive || c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

    public Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default) =>
        db.Categories.AsNoTracking()
            .AnyAsync(c => c.Name.ToLower() == name.Trim().ToLower() && (excludeId == null || c.Id != excludeId), ct);

    public Task<bool> HasProductsAsync(Guid categoryId, CancellationToken ct = default) =>
        db.Products.AsNoTracking().AnyAsync(p => p.CategoryId == categoryId, ct);

    public void Add(Category category) => db.Categories.Add(category);
    public void Remove(Category category) => db.Categories.Remove(category);
}
