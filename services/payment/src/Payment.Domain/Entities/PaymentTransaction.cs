using BuildingBlocks.Core.Domain;
using BuildingBlocks.Core.Exceptions;
using Payment.Domain.Enums;
using Payment.Domain.Events;

namespace Payment.Domain.Entities;

/// <summary>
/// Giao dịch thanh toán của 1 đơn hàng. IdempotencyKey đảm bảo retry mạng
/// không tạo ra 2 lần trừ tiền — bài toán kinh điển của thanh toán phân tán.
/// </summary>
public sealed class PaymentTransaction : AggregateRoot
{
    private readonly List<Refund> _refunds = new();

    private PaymentTransaction() { }

    public Guid OrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Money Amount { get; private set; } = Money.Zero();
    public PaymentMethod Method { get; private set; }
    public PaymentStatus Status { get; private set; } = PaymentStatus.Pending;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string? TransactionRef { get; private set; }
    public string? FailureReason { get; private set; }
    public decimal RefundedAmount { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();
    public decimal RefundableAmount => Amount.Amount - RefundedAmount;

    public static PaymentTransaction Create(Guid orderId, Guid customerId, decimal amount,
        string currency, PaymentMethod method, string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainException("payment.missing_idempotency_key", "Thiếu idempotency key.");

        return new PaymentTransaction
        {
            OrderId = orderId,
            CustomerId = customerId,
            Amount = Money.Of(amount, currency),
            Method = method,
            IdempotencyKey = idempotencyKey
        };
    }

    public void Authorize(string transactionRef)
    {
        EnsureStatus(PaymentStatus.Pending);
        Status = PaymentStatus.Authorized;
        TransactionRef = transactionRef;
        Touch();
    }

    /// <summary>Trừ tiền thật. COD đi thẳng từ Pending sang Captured khi giao hàng xong.</summary>
    public void Capture(string transactionRef)
    {
        if (Status is not (PaymentStatus.Pending or PaymentStatus.Authorized))
            throw new DomainException("payment.invalid_state",
                $"Không thể capture khi đang ở trạng thái {Status}.");

        Status = PaymentStatus.Captured;
        TransactionRef = transactionRef;
        CompletedAtUtc = DateTime.UtcNow;
        FailureReason = null;
        Touch();

        Raise(new PaymentSucceededDomainEvent(Id, OrderId, CustomerId,
            Amount.Amount, Amount.Currency, transactionRef));
    }

    public void Fail(string reason)
    {
        if (Status is PaymentStatus.Captured or PaymentStatus.Refunded)
            throw new DomainException("payment.invalid_state",
                $"Giao dịch đã {Status}, không thể đánh dấu thất bại.");

        Status = PaymentStatus.Failed;
        FailureReason = reason;
        CompletedAtUtc = DateTime.UtcNow;
        Touch();

        Raise(new PaymentFailedDomainEvent(Id, OrderId, CustomerId, Amount.Amount, reason));
    }

    public Refund IssueRefund(decimal amount, string reason)
    {
        if (Status is not (PaymentStatus.Captured or PaymentStatus.PartiallyRefunded))
            throw new DomainException("payment.not_refundable",
                "Chỉ hoàn tiền được giao dịch đã trừ tiền thành công.");

        if (amount <= 0 || amount > RefundableAmount)
            throw new DomainException("payment.invalid_refund_amount",
                $"Số tiền hoàn phải trong khoảng (0, {RefundableAmount}].");

        var refund = Refund.Create(Id, amount, Amount.Currency, reason);
        _refunds.Add(refund);
        RefundedAmount += amount;

        Status = RefundedAmount >= Amount.Amount ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
        Touch();

        Raise(new PaymentRefundedDomainEvent(Id, OrderId, amount, reason));
        return refund;
    }

    private void EnsureStatus(PaymentStatus expected)
    {
        if (Status != expected)
            throw new DomainException("payment.invalid_state",
                $"Giao dịch đang ở {Status}, cần {expected}.");
    }
}

public sealed class Refund : Entity
{
    private Refund() { }

    public Guid PaymentId { get; private set; }
    public Money Amount { get; private set; } = Money.Zero();
    public string Reason { get; private set; } = string.Empty;
    public string? TransactionRef { get; private set; }

    internal static Refund Create(Guid paymentId, decimal amount, string currency, string reason) => new()
    {
        PaymentId = paymentId,
        Amount = Money.Of(amount, currency),
        Reason = reason,
        TransactionRef = $"RFD-{Guid.NewGuid():N}"[..20]
    };
}
