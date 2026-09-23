using Inventory.Domain.Entities;
using Inventory.Domain.Enums;

namespace Inventory.Application.Common;

public sealed record StockItemDto(
    Guid Id, Guid ProductId, string Sku, string WarehouseCode,
    int QuantityOnHand, int QuantityReserved, int QuantityAvailable,
    int ReorderLevel, bool IsLowStock, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);

public sealed record StockMovementDto(
    Guid Id, Guid ProductId, string Sku, StockMovementType Type,
    int Quantity, int QuantityAfter, string? Reference, string? Note, DateTime CreatedAtUtc);

public sealed record ReservationDto(
    Guid Id, Guid OrderId, ReservationStatus Status, DateTime ExpiresAtUtc,
    DateTime? CompletedAtUtc, IReadOnlyList<ReservationLineDto> Lines);

public sealed record ReservationLineDto(Guid ProductId, int Quantity);

public sealed record ReserveStockResult(
    bool Success, Guid? ReservationId, string Message,
    IReadOnlyList<InsufficientStockDto> Insufficient);

public sealed record InsufficientStockDto(Guid ProductId, int Requested, int Available);

public static class InventoryMapping
{
    public static StockItemDto ToDto(this StockItem s) => new(
        s.Id, s.ProductId, s.Sku, s.WarehouseCode, s.QuantityOnHand, s.QuantityReserved,
        s.QuantityAvailable, s.ReorderLevel, s.IsLowStock, s.CreatedAtUtc, s.UpdatedAtUtc);

    public static StockMovementDto ToDto(this StockMovement m) => new(
        m.Id, m.ProductId, m.Sku, m.Type, m.Quantity, m.QuantityAfter, m.Reference, m.Note, m.CreatedAtUtc);

    public static ReservationDto ToDto(this Reservation r) => new(
        r.Id, r.OrderId, r.Status, r.ExpiresAtUtc, r.CompletedAtUtc,
        r.Lines.Select(l => new ReservationLineDto(l.ProductId, l.Quantity)).ToList());
}

public static class CacheKeys
{
    public const string StockPrefix = "stock:";
    public static string Stock(Guid productId) => $"{StockPrefix}product:{productId}";
    public static string StockLock(Guid productId) => $"stock:{productId}";
}
