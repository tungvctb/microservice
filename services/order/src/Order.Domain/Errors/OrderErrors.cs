using BuildingBlocks.Core.Results;

namespace Order.Domain.Errors;

public static class OrderErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("order.not_found", $"Không tìm thấy đơn hàng {id}.");

    public static Error ProductValidationFailed(string detail) =>
        Error.Validation("order.product_invalid", detail);

    public static Error OutOfStock(string detail) =>
        Error.Conflict("order.out_of_stock", detail);

    public static Error CatalogUnavailable(string detail) =>
        Error.Failure("order.catalog_unavailable", $"Không kết nối được dịch vụ sản phẩm: {detail}");

    public static Error InventoryUnavailable(string detail) =>
        Error.Failure("order.inventory_unavailable", $"Không kết nối được dịch vụ kho: {detail}");

    public static readonly Error Forbidden =
        Error.Forbidden("order.forbidden", "Bạn không có quyền truy cập đơn hàng này.");
}
