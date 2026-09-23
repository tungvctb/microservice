using BuildingBlocks.Core.Pagination;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Order.Domain.Entities;
using Order.Domain.Enums;
using Order.Domain.Repositories;

namespace Order.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository(OrderDbContext db) : IOrderRepository
{
    public Task<OrderAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<OrderAggregate?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default) =>
        db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, ct);

    public async Task<PagedResult<OrderAggregate>> SearchAsync(OrderFilter filter, CancellationToken ct = default)
    {
        var query = db.Orders.AsNoTracking().Include(o => o.Lines).AsQueryable();

        query = query
            .WhereIf(filter.CustomerId.HasValue, o => o.CustomerId == filter.CustomerId!.Value)
            .WhereIf(filter.Status.HasValue, o => o.Status == filter.Status!.Value)
            .WhereIf(filter.FromUtc.HasValue, o => o.CreatedAtUtc >= filter.FromUtc!.Value)
            .WhereIf(filter.ToUtc.HasValue, o => o.CreatedAtUtc <= filter.ToUtc!.Value);

        if (!string.IsNullOrWhiteSpace(filter.OrderNumber))
            query = query.Where(o => EF.Functions.ILike(o.OrderNumber, $"%{filter.OrderNumber.Trim()}%"));

        return await query.OrderByDescending(o => o.CreatedAtUtc).ToPagedResultAsync(filter, ct);
    }

    public async Task<OrderStatistics> GetStatisticsAsync(Guid? customerId, CancellationToken ct = default)
    {
        var query = db.Orders.AsNoTracking()
            .WhereIf(customerId.HasValue, o => o.CustomerId == customerId!.Value);

        // Một lượt duy nhất xuống DB thay vì 5 câu COUNT riêng lẻ.
        var grouped = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Pending = g.Count(o => o.Status == OrderStatus.Pending
                                       || o.Status == OrderStatus.AwaitingPayment),
                Confirmed = g.Count(o => o.Status == OrderStatus.Confirmed
                                         || o.Status == OrderStatus.Shipped
                                         || o.Status == OrderStatus.Completed),
                Cancelled = g.Count(o => o.Status == OrderStatus.Cancelled),
                Revenue = g.Where(o => o.Status == OrderStatus.Confirmed
                                       || o.Status == OrderStatus.Shipped
                                       || o.Status == OrderStatus.Completed)
                    .Sum(o => (decimal?)o.TotalAmount.Amount) ?? 0m
            })
            .FirstOrDefaultAsync(ct);

        return grouped is null
            ? new OrderStatistics(0, 0, 0, 0, 0m)
            : new OrderStatistics(grouped.Total, grouped.Pending, grouped.Confirmed,
                grouped.Cancelled, grouped.Revenue);
    }

    public void Add(OrderAggregate order) => db.Orders.Add(order);
}
