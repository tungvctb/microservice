using BuildingBlocks.Core.Pagination;
using Payment.Domain.Entities;
using Payment.Domain.Enums;

namespace Payment.Domain.Repositories;

public sealed record PaymentFilter : PageRequest
{
    public Guid? OrderId { get; init; }
    public Guid? CustomerId { get; init; }
    public PaymentStatus? Status { get; init; }
    public PaymentMethod? Method { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
}

public interface IPaymentRepository
{
    Task<PaymentTransaction?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PaymentTransaction?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default);
    Task<PagedResult<PaymentTransaction>> SearchAsync(PaymentFilter filter, CancellationToken ct = default);
    void Add(PaymentTransaction payment);
}
