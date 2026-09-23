using BuildingBlocks.Core.Domain;
using BuildingBlocks.Core.Exceptions;

namespace Order.Domain.Entities;

/// <summary>
/// Dòng hàng chụp lại tên/giá tại thời điểm đặt. Giá sau này đổi ở catalog
/// cũng không làm thay đổi đơn hàng cũ — đây là dữ liệu lịch sử, không phải tham chiếu.
/// </summary>
public sealed class OrderLine : Entity
{
    private OrderLine() { }

    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; } = Money.Zero();

    public Money LineTotal => UnitPrice.Multiply(Quantity);

    internal static OrderLine Create(Guid orderId, Guid productId, string sku, string productName,
        int quantity, decimal unitPrice, string currency)
    {
        if (quantity <= 0)
            throw new DomainException("order.invalid_quantity", "Số lượng phải lớn hơn 0.");

        return new OrderLine
        {
            OrderId = orderId,
            ProductId = productId,
            Sku = sku,
            ProductName = productName,
            Quantity = quantity,
            UnitPrice = Money.Of(unitPrice, currency)
        };
    }
}
