namespace Inventory.Domain.Enums;

public enum ReservationStatus { Pending = 0, Committed = 1, Released = 2, Expired = 3 }

public enum StockMovementType
{
    Inbound = 0,     // nhập kho
    Outbound = 1,    // xuất kho (khi commit reservation)
    Reserved = 2,    // giữ chỗ
    Released = 3,    // nhả giữ chỗ
    Adjustment = 4   // kiểm kê / điều chỉnh
}
