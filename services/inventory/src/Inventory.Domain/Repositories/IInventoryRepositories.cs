using BuildingBlocks.Core.Pagination;
using Inventory.Domain.Entities;

namespace Inventory.Domain.Repositories;

public sealed record StockFilter : PageRequest
{
    public string? Search { get; init; }
    public bool? LowStockOnly { get; init; }
    public string? WarehouseCode { get; init; }
}

public interface IStockRepository
{
    Task<StockItem?> GetByProductIdAsync(Guid productId, CancellationToken ct = default);
    Task<IReadOnlyList<StockItem>> GetByProductIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);
    Task<PagedResult<StockItem>> SearchAsync(StockFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<StockItem>> GetLowStockAsync(CancellationToken ct = default);
    void Add(StockItem item);
    void Remove(StockItem item);
}

public interface IReservationRepository
{
    Task<Reservation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Reservation?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    Task<IReadOnlyList<Reservation>> GetExpiredAsync(int limit, CancellationToken ct = default);
    void Add(Reservation reservation);
}

public interface IStockMovementRepository
{
    Task<PagedResult<StockMovement>> GetByProductAsync(Guid productId, PageRequest page, CancellationToken ct = default);
    void Add(StockMovement movement);
}
