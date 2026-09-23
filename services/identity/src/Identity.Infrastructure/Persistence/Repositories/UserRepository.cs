using BuildingBlocks.Core.Pagination;
using BuildingBlocks.Infrastructure.Persistence;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(IdentityDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Users.Include(u => u.RefreshTokens).FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        db.Users.Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Email == email.Trim().ToLower(), ct);

    public Task<User?> GetByRefreshTokenAsync(string token, CancellationToken ct = default) =>
        db.Users.Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.RefreshTokens.Any(t => t.Token == token), ct);

    public async Task<PagedResult<User>> SearchAsync(UserFilter filter, CancellationToken ct = default)
    {
        var query = db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = $"%{filter.Search.Trim()}%";
            query = query.Where(u => EF.Functions.ILike(u.Email, term) || EF.Functions.ILike(u.FullName, term));
        }

        query = query
            .WhereIf(filter.IsActive.HasValue, u => u.IsActive == filter.IsActive!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(filter.Role),
                u => EF.Property<List<string>>(u, "_roles").Contains(filter.Role!));

        return await query.OrderByDescending(u => u.CreatedAtUtc).ToPagedResultAsync(filter, ct);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) =>
        db.Users.AsNoTracking().AnyAsync(u => u.Email == email.Trim().ToLower(), ct);

    public void Add(User user) => db.Users.Add(user);
}
