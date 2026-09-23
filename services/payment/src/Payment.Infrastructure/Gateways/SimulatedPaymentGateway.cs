using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Payment.Domain.Repositories;

namespace Payment.Infrastructure.Gateways;

public sealed class PaymentGatewayOptions
{
    public const string SectionName = "PaymentGateway";

    /// <summary>Độ trễ giả lập để thấy rõ tính bất đồng bộ của saga.</summary>
    public int MinLatencyMs { get; set; } = 200;
    public int MaxLatencyMs { get; set; } = 800;

    /// <summary>Tỉ lệ thất bại ngẫu nhiên (0.0 - 1.0) để thử nhánh bồi hoàn.</summary>
    public double FailureRate { get; set; } = 0.1;

    /// <summary>Đơn có tổng tiền chia hết cho số này sẽ luôn thất bại — dùng để test có chủ đích.</summary>
    public decimal ForceFailAmountMultiple { get; set; } = 13;
}

/// <summary>
/// Cổng thanh toán giả lập. Có 2 cách kích hoạt nhánh thất bại:
/// ngẫu nhiên theo FailureRate, hoặc chủ động bằng số tiền chia hết cho ForceFailAmountMultiple.
/// </summary>
public sealed class SimulatedPaymentGateway(
    IOptions<PaymentGatewayOptions> options,
    ILogger<SimulatedPaymentGateway> logger) : IPaymentGateway
{
    private readonly PaymentGatewayOptions _options = options.Value;

    public async Task<GatewayResult> ChargeAsync(GatewayChargeRequest request, CancellationToken ct = default)
    {
        await SimulateLatencyAsync(ct);

        if (_options.ForceFailAmountMultiple > 0 &&
            request.Amount % _options.ForceFailAmountMultiple == 0)
        {
            logger.LogWarning("Cổng thanh toán từ chối đơn {OrderId} (quy tắc test theo số tiền)", request.OrderId);
            return new GatewayResult(false, null, "Thẻ bị từ chối bởi ngân hàng phát hành (mã 51).");
        }

        if (Random.Shared.NextDouble() < _options.FailureRate)
        {
            logger.LogWarning("Cổng thanh toán từ chối đơn {OrderId} (lỗi ngẫu nhiên giả lập)", request.OrderId);
            return new GatewayResult(false, null, "Giao dịch bị từ chối, vui lòng thử phương thức khác.");
        }

        var reference = $"TXN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..28];
        logger.LogInformation("Thanh toán thành công đơn {OrderId}, mã giao dịch {Ref}",
            request.OrderId, reference);

        return new GatewayResult(true, reference, null);
    }

    public async Task<GatewayResult> RefundAsync(string transactionRef, decimal amount, CancellationToken ct = default)
    {
        await SimulateLatencyAsync(ct);
        logger.LogInformation("Hoàn {Amount} cho giao dịch {Ref}", amount, transactionRef);
        return new GatewayResult(true, $"RFD-{Guid.NewGuid():N}"[..20], null);
    }

    private Task SimulateLatencyAsync(CancellationToken ct) =>
        Task.Delay(Random.Shared.Next(_options.MinLatencyMs, _options.MaxLatencyMs + 1), ct);
}
