using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Application.Messaging;
using BuildingBlocks.Contracts.IntegrationEvents;
using Microsoft.Extensions.Logging;
using Payment.Application.Payments.Commands;
using Payment.Domain.Enums;

namespace Payment.Application.EventHandlers;

/// <summary>
/// Order đặt hàng xong (đã giữ chỗ kho) → Payment tự động xử lý thanh toán.
/// Đây là mắt xích bất đồng bộ của saga: Order không chờ Payment, nó nghe kết quả qua event.
/// IdempotencyKey lấy từ EventId nên Kafka giao lại message cũng không trừ tiền 2 lần.
/// </summary>
public sealed class OrderPlacedHandler(IDispatcher dispatcher, ILogger<OrderPlacedHandler> logger)
    : IIntegrationEventHandler<OrderPlacedIntegrationEvent>
{
    public async Task HandleAsync(OrderPlacedIntegrationEvent @event, CancellationToken ct)
    {
        var method = Enum.TryParse<PaymentMethod>(@event.PaymentMethod, ignoreCase: true, out var parsed)
            ? parsed
            : PaymentMethod.CreditCard;

        var result = await dispatcher.Send(new ProcessPaymentCommand(
            @event.OrderId,
            @event.CustomerId,
            @event.TotalAmount,
            @event.Currency,
            method,
            IdempotencyKey: $"order-{@event.OrderId}"), ct);

        if (result.IsFailure)
            logger.LogError("Xử lý thanh toán cho đơn {OrderId} thất bại: {Error}",
                @event.OrderId, result.Error.Message);
        else
            logger.LogInformation("Đơn {OrderNumber} → thanh toán {Status}",
                @event.OrderNumber, result.Value.Status);
    }
}
