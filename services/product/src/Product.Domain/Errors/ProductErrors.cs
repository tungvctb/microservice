using BuildingBlocks.Core.Results;

namespace Product.Domain.Errors;

public static class ProductErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("product.not_found", $"Không tìm thấy sản phẩm {id}.");

    public static Error SkuDuplicated(string sku) =>
        Error.Conflict("product.sku_duplicated", $"SKU '{sku}' đã tồn tại.");

    public static readonly Error InvalidCategory =
        Error.Validation("product.invalid_category", "Danh mục không tồn tại hoặc đã ngừng hoạt động.");
}

public static class CategoryErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("category.not_found", $"Không tìm thấy danh mục {id}.");

    public static Error NameDuplicated(string name) =>
        Error.Conflict("category.name_duplicated", $"Danh mục '{name}' đã tồn tại.");

    public static readonly Error HasProducts =
        Error.Conflict("category.has_products", "Không thể xóa danh mục đang chứa sản phẩm.");
}
