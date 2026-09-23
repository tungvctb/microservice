using BuildingBlocks.Core.Pagination;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence;

public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query,
        PageRequest request, CancellationToken ct = default)
    {
        var total = await query.LongCountAsync(ct);
        if (total == 0) return PagedResult<T>.Empty(request.Page, request.PageSize);

        var items = await query.Skip(request.Skip).Take(request.PageSize).ToListAsync(ct);
        return new PagedResult<T>(items, request.Page, request.PageSize, total);
    }

    public static IQueryable<T> WhereIf<T>(this IQueryable<T> query, bool condition,
        System.Linq.Expressions.Expression<Func<T, bool>> predicate) =>
        condition ? query.Where(predicate) : query;
}
