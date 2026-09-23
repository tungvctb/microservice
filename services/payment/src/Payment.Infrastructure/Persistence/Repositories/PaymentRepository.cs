using BuildingBlocks.Core.Pagination;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Payment.Domain.Entities;
using Payment.Domain.Repositories;

namespace Payment.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository(PaymentDbContext db) : IPaymentRepository
{
    public Task<PaymentTransaction?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Payments.Include(p => p.Refunds).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<PaymentTransaction?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default) =>
        db.Payments.Include(p => p.Refunds).FirstOrDefaultAsync(p => p.OrderId == orderId, ct);

    public Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default) =>
        db.Payments.Include(p => p.Refunds).FirstOrDefaultAsync(p => p.IdempotencyKey == key, ct);

    public async Task<PagedResult<PaymentTransaction>> SearchAsync(PaymentFilter filter, CancellationToken ct = default)
    {
        var query = db.Payments.AsNoTracking().Include(p => p.Refunds).AsQueryable();

        query = query
            .WhereIf(filter.OrderId.HasValue, p => p.OrderId == filter.OrderId!.Value)
            .WhereIf(filter.CustomerId.HasValue, p => p.CustomerId == filter.CustomerId!.Value)
            .WhereIf(filter.Status.HasValue, p => p.Status == filter.Status!.Value)
            .WhereIf(filter.Method.HasValue, p => p.Method == filter.Method!.Value)
            .WhereIf(filter.FromUtc.HasValue, p => p.CreatedAtUtc >= filter.FromUtc!.Value)
            .WhereIf(filter.ToUtc.HasValue, p => p.CreatedAtUtc <= filter.ToUtc!.Value);

        return await query.OrderByDescending(p => p.CreatedAtUtc).ToPagedResultAsync(filter, ct);
    }

    public void Add(PaymentTransaction payment) => db.Payments.Add(payment);
}
