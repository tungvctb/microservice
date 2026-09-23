using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Application.Messaging;
using BuildingBlocks.Contracts.IntegrationEvents;
using Microsoft.Extensions.Logging;
using Order.Application.Orders.Commands;

namespace Order.Application.EventHandlers;

/// <summary>Bước 4 của saga — nhánh thành công.</summary>
public sealed class PaymentSucceededHandler(IDispatcher dispatcher, ILogger<PaymentSucceededHandler> logger)
    : IIntegrationEventHandler<PaymentSucceededIntegrationEvent>
{
    public async Task HandleAsync(PaymentSucceededIntegrationEvent @event, CancellationToken ct)
    {
        var result = await dispatcher.Send(new ConfirmOrderPaymentCommand(
            @event.OrderId, @event.PaymentId, @event.TransactionRef), ct);

        if (result.IsFailure)
            logger.LogError("Xác nhận đơn {OrderId} sau thanh toán thất bại: {Error}",
                @event.OrderId, result.Error.Message);
    }
}

/// <summary>
/// Bước 4 — nhánh bồi hoàn. Hủy đơn phát tiếp event OrderCancelled,
/// Inventory nghe và nhả giữ chỗ. Chuỗi bồi hoàn chạy hoàn toàn qua Kafka.
/// </summary>
public sealed class PaymentFailedHandler(IDispatcher dispatcher, ILogger<PaymentFailedHandler> logger)
    : IIntegrationEventHandler<PaymentFailedIntegrationEvent>
{
    public async Task HandleAsync(PaymentFailedIntegrationEvent @event, CancellationToken ct)
    {
        logger.LogWarning("Đơn {OrderId} thanh toán thất bại ({Reason}) — bắt đầu bồi hoàn",
            @event.OrderId, @event.Reason);

        var result = await dispatcher.Send(new CancelOrderCommand(
            @event.OrderId, $"Thanh toán thất bại: {@event.Reason}", BySystem: true), ct);

        if (result.IsFailure)
            logger.LogError("Hủy đơn {OrderId} thất bại: {Error}", @event.OrderId, result.Error.Message);
    }
}
