using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Contracts.IntegrationEvents;
using BuildingBlocks.Application.Messaging;
using Inventory.Application.Reservations.Commands;
using Inventory.Application.Stocks.Commands;
using Microsoft.Extensions.Logging;

namespace Inventory.Application.EventHandlers;

/// <summary>
/// Catalog tạo sản phẩm mới → Inventory tự tạo bản ghi tồn kho tương ứng.
/// Đây là lý do 2 service tách nhau mà vẫn đồng bộ: qua event, không gọi trực tiếp.
/// </summary>
public sealed class ProductCreatedHandler(IDispatcher dispatcher, ILogger<ProductCreatedHandler> logger)
    : IIntegrationEventHandler<ProductCreatedIntegrationEvent>
{
    public async Task HandleAsync(ProductCreatedIntegrationEvent @event, CancellationToken ct)
    {
        var result = await dispatcher.Send(new CreateStockItemCommand(
            @event.ProductId, @event.Sku, @event.InitialStock, ReorderLevel: 10), ct);

        if (result.IsFailure && result.Error.Code != "stock.duplicated")
            logger.LogWarning("Tạo tồn kho cho sản phẩm {Sku} thất bại: {Error}", @event.Sku, result.Error.Message);
        else
            logger.LogInformation("Đã khởi tạo tồn kho cho sản phẩm mới {Sku} ({Qty})",
                @event.Sku, @event.InitialStock);
    }
}

/// <summary>Thanh toán thành công → chốt giữ chỗ, trừ kho thật.</summary>
public sealed class PaymentSucceededHandler(IDispatcher dispatcher, ILogger<PaymentSucceededHandler> logger)
    : IIntegrationEventHandler<PaymentSucceededIntegrationEvent>
{
    public async Task HandleAsync(PaymentSucceededIntegrationEvent @event, CancellationToken ct)
    {
        var result = await dispatcher.Send(new CommitReservationCommand(@event.OrderId), ct);

        if (result.IsFailure)
            logger.LogWarning("Chốt giữ chỗ cho đơn {OrderId} thất bại: {Error}",
                @event.OrderId, result.Error.Message);
    }
}

/// <summary>Đơn bị hủy → nhả hàng. Bồi hoàn của saga.</summary>
public sealed class OrderCancelledHandler(IDispatcher dispatcher, ILogger<OrderCancelledHandler> logger)
    : IIntegrationEventHandler<OrderCancelledIntegrationEvent>
{
    public async Task HandleAsync(OrderCancelledIntegrationEvent @event, CancellationToken ct)
    {
        var result = await dispatcher.Send(
            new ReleaseReservationCommand(@event.OrderId, @event.Reason), ct);

        if (result.IsFailure && result.Error.Code != "reservation.not_found")
            logger.LogWarning("Nhả giữ chỗ cho đơn {OrderId} thất bại: {Error}",
                @event.OrderId, result.Error.Message);
    }
}
