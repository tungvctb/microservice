using BuildingBlocks.Core.Domain;
using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

/// <summary>Nhật ký biến động kho — phục vụ đối soát và truy vết.</summary>
public sealed class StockMovement : Entity
{
    private StockMovement() { }

    public Guid ProductId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public StockMovementType Type { get; private set; }
    public int Quantity { get; private set; }
    public int QuantityAfter { get; private set; }
    public string? Reference { get; private set; }
    public string? Note { get; private set; }

    public static StockMovement Log(Guid productId, string sku, StockMovementType type,
        int quantity, int quantityAfter, string? reference = null, string? note = null) => new()
    {
        ProductId = productId,
        Sku = sku,
        Type = type,
        Quantity = quantity,
        QuantityAfter = quantityAfter,
        Reference = reference,
        Note = note
    };
}
