using BuildingBlocks.Core.Pagination;
using BuildingBlocks.Infrastructure.Persistence;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Persistence.Repositories;

public sealed class StockRepository(InventoryDbContext db) : IStockRepository
{
    public Task<StockItem?> GetByProductIdAsync(Guid productId, CancellationToken ct = default) =>
        db.StockItems.FirstOrDefaultAsync(s => s.ProductId == productId, ct);

    public async Task<IReadOnlyList<StockItem>> GetByProductIdsAsync(IReadOnlyCollection<Guid> productIds,
        CancellationToken ct = default) =>
        await db.StockItems.Where(s => productIds.Contains(s.ProductId)).ToListAsync(ct);

    public async Task<PagedResult<StockItem>> SearchAsync(StockFilter filter, CancellationToken ct = default)
    {
        var query = db.StockItems.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
            query = query.Where(s => EF.Functions.ILike(s.Sku, $"%{filter.Search.Trim()}%"));

        query = query.WhereIf(!string.IsNullOrWhiteSpace(filter.WarehouseCode),
            s => s.WarehouseCode == filter.WarehouseCode);

        // Tính trên cột thay vì property computed để Postgres lọc được ở tầng SQL.
        if (filter.LowStockOnly == true)
            query = query.Where(s => s.QuantityOnHand - s.QuantityReserved <= s.ReorderLevel);

        query = query.OrderBy(s => s.Sku);
        return await query.ToPagedResultAsync(filter, ct);
    }

    public async Task<IReadOnlyList<StockItem>> GetLowStockAsync(CancellationToken ct = default) =>
        await db.StockItems.AsNoTracking()
            .Where(s => s.QuantityOnHand - s.QuantityReserved <= s.ReorderLevel)
            .OrderBy(s => s.QuantityOnHand - s.QuantityReserved)
            .ToListAsync(ct);

    public void Add(StockItem item) => db.StockItems.Add(item);
    public void Remove(StockItem item) => db.StockItems.Remove(item);
}

public sealed class ReservationRepository(InventoryDbContext db) : IReservationRepository
{
    public Task<Reservation?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Reservations.Include(r => r.Lines).FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<Reservation?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default) =>
        db.Reservations.Include(r => r.Lines).FirstOrDefaultAsync(r => r.OrderId == orderId, ct);

    public async Task<IReadOnlyList<Reservation>> GetExpiredAsync(int limit, CancellationToken ct = default) =>
        await db.Reservations.Include(r => r.Lines)
            .Where(r => r.Status == ReservationStatus.Pending && r.ExpiresAtUtc < DateTime.UtcNow)
            .OrderBy(r => r.ExpiresAtUtc)
            .Take(limit)
            .ToListAsync(ct);

    public void Add(Reservation reservation) => db.Reservations.Add(reservation);
}

public sealed class StockMovementRepository(InventoryDbContext db) : IStockMovementRepository
{
    public Task<PagedResult<StockMovement>> GetByProductAsync(Guid productId, PageRequest page,
        CancellationToken ct = default) =>
        db.StockMovements.AsNoTracking()
            .Where(m => m.ProductId == productId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToPagedResultAsync(page, ct);

    public void Add(StockMovement movement) => db.StockMovements.Add(movement);
}
