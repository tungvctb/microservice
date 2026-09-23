using BuildingBlocks.Core.Domain;
using BuildingBlocks.Core.Exceptions;
using Order.Domain.Enums;
using Order.Domain.Events;
using Order.Domain.ValueObjects;

namespace Order.Domain.Entities;

/// <summary>
/// Aggregate root điều phối saga. Trạng thái đơn hàng là nguồn sự thật duy nhất
/// cho biết saga đang ở bước nào và còn cần bồi hoàn gì.
/// </summary>
public sealed class OrderAggregate : AggregateRoot
{
    private readonly List<OrderLine> _lines = new();

    private OrderAggregate() { }

    public string OrderNumber { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public string CustomerEmail { get; private set; } = string.Empty;
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;
    public PaymentMethodType PaymentMethod { get; private set; }
    public ShippingAddress ShippingAddress { get; private set; } = null!;
    public Money TotalAmount { get; private set; } = Money.Zero();
    public Guid? ReservationId { get; private set; }
    public Guid? PaymentId { get; private set; }
    public string? PaymentRef { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTime? ConfirmedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();

    public static OrderAggregate Create(Guid customerId, string customerEmail, ShippingAddress address,
        PaymentMethodType paymentMethod, string currency = "VND")
    {
        if (customerId == Guid.Empty)
            throw new DomainException("order.customer_required", "Thiếu thông tin khách hàng.");

        return new OrderAggregate
        {
            OrderNumber = GenerateOrderNumber(),
            CustomerId = customerId,
            CustomerEmail = customerEmail,
            ShippingAddress = address,
            PaymentMethod = paymentMethod,
            TotalAmount = Money.Zero(currency)
        };
    }

    public void AddLine(Guid productId, string sku, string productName, int quantity,
        decimal unitPrice, string currency)
    {
        EnsureMutable();

        var existing = _lines.FirstOrDefault(l => l.ProductId == productId);
        if (existing is not null)
            throw new DomainException("order.duplicate_line",
                $"Sản phẩm {sku} đã có trong đơn — hãy gộp số lượng ở tầng gọi.");

        _lines.Add(OrderLine.Create(Id, productId, sku, productName, quantity, unitPrice, currency));
        RecalculateTotal(currency);
    }

    /// <summary>Kho đã giữ chỗ xong → chuyển sang chờ thanh toán và phát event cho Payment service.</summary>
    public void MarkStockReserved(Guid reservationId)
    {
        EnsureStatus(OrderStatus.Pending);

        if (_lines.Count == 0)
            throw new DomainException("order.empty", "Đơn hàng phải có ít nhất 1 sản phẩm.");

        ReservationId = reservationId;
        Status = OrderStatus.AwaitingPayment;
        Touch();

        Raise(new OrderPlacedDomainEvent(Id, OrderNumber, CustomerId, CustomerEmail,
            TotalAmount.Amount, TotalAmount.Currency, PaymentMethod.ToString(),
            _lines.Select(l => (l.ProductId, l.Sku, l.ProductName, l.Quantity, l.UnitPrice.Amount)).ToList()));
    }

    public void MarkPaid(Guid paymentId, string paymentRef)
    {
        if (Status is OrderStatus.Confirmed or OrderStatus.Completed) return;   // idempotent

        EnsureStatus(OrderStatus.AwaitingPayment);

        PaymentId = paymentId;
        PaymentRef = paymentRef;
        Status = OrderStatus.Confirmed;
        ConfirmedAtUtc = DateTime.UtcNow;
        Touch();

        Raise(new OrderConfirmedDomainEvent(Id, OrderNumber, CustomerId, TotalAmount.Amount));
    }

    public void MarkShipped()
    {
        EnsureStatus(OrderStatus.Confirmed);
        Status = OrderStatus.Shipped;
        Touch();
    }

    public void Complete()
    {
        if (Status is not (OrderStatus.Shipped or OrderStatus.Confirmed))
            throw new DomainException("order.invalid_state",
                $"Chỉ hoàn tất được đơn đã xác nhận/đã giao, hiện tại: {Status}.");

        Status = OrderStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        Touch();

        Raise(new OrderCompletedDomainEvent(Id, OrderNumber, CustomerId));
    }

    /// <summary>Hủy đơn. Event phát ra mang theo ReservationId để Inventory biết cần nhả gì.</summary>
    public void Cancel(string reason)
    {
        if (Status is OrderStatus.Cancelled) return;   // idempotent

        if (Status is OrderStatus.Completed or OrderStatus.Shipped)
            throw new DomainException("order.cannot_cancel",
                $"Không thể hủy đơn đang ở trạng thái {Status}.");

        Status = OrderStatus.Cancelled;
        CancellationReason = reason;
        Touch();

        Raise(new OrderCancelledDomainEvent(Id, OrderNumber, CustomerId, reason, ReservationId));
    }

    private void RecalculateTotal(string currency)
    {
        var total = _lines.Aggregate(Money.Zero(currency), (sum, line) => sum.Add(line.LineTotal));
        TotalAmount = total;
    }

    private void EnsureMutable()
    {
        if (Status != OrderStatus.Pending)
            throw new DomainException("order.immutable",
                $"Không thể sửa dòng hàng khi đơn đã ở trạng thái {Status}.");
    }

    private void EnsureStatus(OrderStatus expected)
    {
        if (Status != expected)
            throw new DomainException("order.invalid_state",
                $"Đơn đang ở {Status}, thao tác này yêu cầu {expected}.");
    }

    private static string GenerateOrderNumber() =>
        $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100000, 999999)}";
}
