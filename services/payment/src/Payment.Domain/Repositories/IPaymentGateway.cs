namespace Payment.Domain.Repositories;

/// <summary>
/// Trừu tượng cổng thanh toán. Bản demo dùng gateway giả lập;
/// đổi sang VNPay/Stripe chỉ cần thay implementation, không đụng tới domain.
/// </summary>
public interface IPaymentGateway
{
    Task<GatewayResult> ChargeAsync(GatewayChargeRequest request, CancellationToken ct = default);
    Task<GatewayResult> RefundAsync(string transactionRef, decimal amount, CancellationToken ct = default);
}

public sealed record GatewayChargeRequest(
    Guid OrderId, Guid CustomerId, decimal Amount, string Currency, string Method, string IdempotencyKey);

public sealed record GatewayResult(bool Success, string? TransactionRef, string? FailureReason);
