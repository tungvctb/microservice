using BuildingBlocks.Core.Domain;
using BuildingBlocks.Core.Exceptions;
using Inventory.Domain.Events;

namespace Inventory.Domain.Entities;

/// <summary>
/// Tồn kho của 1 sản phẩm tại 1 kho. QuantityAvailable = OnHand - Reserved:
/// hàng đã giữ chỗ vẫn nằm trong kho nhưng không bán được cho đơn khác.
/// </summary>
public sealed class StockItem : AggregateRoot
{
    private StockItem() { }

    public Guid ProductId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string WarehouseCode { get; private set; } = "MAIN";
    public int QuantityOnHand { get; private set; }
    public int QuantityReserved { get; private set; }
    public int ReorderLevel { get; private set; } = 10;

    public int QuantityAvailable => QuantityOnHand - QuantityReserved;
    public bool IsLowStock => QuantityAvailable <= ReorderLevel;

    public static StockItem Create(Guid productId, string sku, int initialQuantity = 0,
        int reorderLevel = 10, string warehouseCode = "MAIN")
    {
        if (initialQuantity < 0)
            throw new DomainException("stock.negative_quantity", "Số lượng khởi tạo không được âm.");

        var item = new StockItem
        {
            ProductId = productId,
            Sku = sku.ToUpperInvariant(),
            QuantityOnHand = initialQuantity,
            ReorderLevel = reorderLevel,
            WarehouseCode = warehouseCode
        };

        item.Raise(new StockLevelChangedDomainEvent(productId, item.Sku,
            item.QuantityOnHand, item.QuantityAvailable));

        return item;
    }

    public void Receive(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("stock.invalid_quantity", "Số lượng nhập phải lớn hơn 0.");

        QuantityOnHand += quantity;
        Touch();
        RaiseLevelChanged();
    }

    /// <summary>Giữ chỗ. Trả false nếu không đủ hàng khả dụng — gọi bên ngoài quyết định cách xử lý.</summary>
    public bool TryReserve(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("stock.invalid_quantity", "Số lượng giữ chỗ phải lớn hơn 0.");

        if (QuantityAvailable < quantity) return false;

        QuantityReserved += quantity;
        Touch();
        RaiseLevelChanged();
        return true;
    }

    /// <summary>Nhả giữ chỗ khi saga thất bại — hàng quay lại trạng thái bán được.</summary>
    public void ReleaseReservation(int quantity)
    {
        if (quantity <= 0) return;
        QuantityReserved = Math.Max(0, QuantityReserved - quantity);
        Touch();
        RaiseLevelChanged();
    }

    /// <summary>Chốt giữ chỗ: trừ hẳn khỏi tồn kho thực (đã thanh toán, chuẩn bị giao).</summary>
    public void CommitReservation(int quantity)
    {
        if (quantity <= 0) return;

        if (QuantityReserved < quantity)
            throw new DomainException("stock.commit_exceeds_reserved",
                $"Không thể chốt {quantity} khi chỉ giữ chỗ {QuantityReserved}.");

        QuantityReserved -= quantity;
        QuantityOnHand -= quantity;
        Touch();
        RaiseLevelChanged();
    }

    public void Adjust(int newQuantityOnHand, string reason)
    {
        if (newQuantityOnHand < QuantityReserved)
            throw new DomainException("stock.adjust_below_reserved",
                $"Tồn kho mới ({newQuantityOnHand}) không được nhỏ hơn số đang giữ chỗ ({QuantityReserved}).");

        QuantityOnHand = newQuantityOnHand;
        Touch();
        RaiseLevelChanged();
    }

    public void SetReorderLevel(int level)
    {
        if (level < 0) throw new DomainException("stock.invalid_reorder", "Ngưỡng cảnh báo không được âm.");
        ReorderLevel = level;
        Touch();
    }

    public void SyncSku(string sku)
    {
        Sku = sku.ToUpperInvariant();
        Touch();
    }

    private void RaiseLevelChanged()
    {
        Raise(new StockLevelChangedDomainEvent(ProductId, Sku, QuantityOnHand, QuantityAvailable));

        if (IsLowStock)
            Raise(new LowStockDetectedDomainEvent(ProductId, Sku, QuantityAvailable, ReorderLevel));
    }
}
