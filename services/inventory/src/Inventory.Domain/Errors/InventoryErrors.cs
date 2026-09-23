using BuildingBlocks.Core.Results;

namespace Inventory.Domain.Errors;

public static class InventoryErrors
{
    public static Error StockNotFound(Guid productId) =>
        Error.NotFound("stock.not_found", $"Chưa có bản ghi tồn kho cho sản phẩm {productId}.");

    public static Error ReservationNotFound(Guid id) =>
        Error.NotFound("reservation.not_found", $"Không tìm thấy giữ chỗ {id}.");

    public static Error InsufficientStock(string detail) =>
        Error.Conflict("stock.insufficient", detail);

    public static readonly Error DuplicateStockItem =
        Error.Conflict("stock.duplicated", "Sản phẩm này đã có bản ghi tồn kho.");

    public static readonly Error LockNotAcquired =
        Error.Conflict("stock.lock_timeout", "Kho đang bận xử lý yêu cầu khác, vui lòng thử lại.");
}
