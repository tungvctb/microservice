using BuildingBlocks.Core.Pagination;
using Order.Domain.Entities;
using Order.Domain.Enums;

namespace Order.Domain.Repositories;

public sealed record OrderFilter : PageRequest
{
    public Guid? CustomerId { get; init; }
    public OrderStatus? Status { get; init; }
    public string? OrderNumber { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
}

public interface IOrderRepository
{
    Task<OrderAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<OrderAggregate?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default);
    Task<PagedResult<OrderAggregate>> SearchAsync(OrderFilter filter, CancellationToken ct = default);
    Task<OrderStatistics> GetStatisticsAsync(Guid? customerId, CancellationToken ct = default);
    void Add(OrderAggregate order);
}

public sealed record OrderStatistics(
    int TotalOrders, int PendingOrders, int ConfirmedOrders, int CancelledOrders, decimal TotalRevenue);
