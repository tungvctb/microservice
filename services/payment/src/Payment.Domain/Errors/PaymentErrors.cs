using BuildingBlocks.Core.Results;

namespace Payment.Domain.Errors;

public static class PaymentErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("payment.not_found", $"Không tìm thấy giao dịch {id}.");

    public static Error NotFoundForOrder(Guid orderId) =>
        Error.NotFound("payment.not_found_for_order", $"Đơn hàng {orderId} chưa có giao dịch thanh toán.");

    public static Error GatewayDeclined(string reason) =>
        Error.Conflict("payment.gateway_declined", reason);
}
