using System.Globalization;
using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Application.Messaging;
using BuildingBlocks.Contracts.IntegrationEvents;
using Notification.Application.Notifications.Commands;
using Notification.Domain.Enums;

namespace Notification.Application.EventHandlers;

/// <summary>
/// Notification là điểm hội tụ của toàn hệ thống: nó nghe mọi topic nghiệp vụ
/// và biến sự kiện kỹ thuật thành thông báo người dùng đọc được.
/// Các service khác không cần biết Notification tồn tại.
/// </summary>
public sealed class OrderPlacedNotificationHandler(IDispatcher dispatcher)
    : IIntegrationEventHandler<OrderPlacedIntegrationEvent>
{
    public Task HandleAsync(OrderPlacedIntegrationEvent @event, CancellationToken ct) =>
        dispatcher.Send(new CreateNotificationCommand(
            RecipientId: @event.CustomerId,
            Title: "Đơn hàng đã được tạo",
            Body: $"Đơn {@event.OrderNumber} trị giá {Format(@event.TotalAmount)} {@event.Currency} " +
                  "đang chờ thanh toán.",
            Severity: NotificationSeverity.Info,
            Link: $"/orders/{@event.OrderId}",
            Metadata: new Dictionary<string, string>
            {
                ["orderId"] = @event.OrderId.ToString(),
                ["orderNumber"] = @event.OrderNumber,
                ["event"] = @event.EventType
            }), ct);

    internal static string Format(decimal amount) => amount.ToString("N0", CultureInfo.InvariantCulture);
}

public sealed class OrderConfirmedNotificationHandler(IDispatcher dispatcher)
    : IIntegrationEventHandler<OrderConfirmedIntegrationEvent>
{
    public Task HandleAsync(OrderConfirmedIntegrationEvent @event, CancellationToken ct) =>
        dispatcher.Send(new CreateNotificationCommand(
            RecipientId: @event.CustomerId,
            Title: "Đặt hàng thành công",
            Body: $"Đơn {@event.OrderNumber} đã được xác nhận và đang chuẩn bị giao.",
            Severity: NotificationSeverity.Success,
            Link: $"/orders/{@event.OrderId}",
            Metadata: new Dictionary<string, string>
            {
                ["orderId"] = @event.OrderId.ToString(),
                ["event"] = @event.EventType
            }), ct);
}

public sealed class OrderCancelledNotificationHandler(IDispatcher dispatcher)
    : IIntegrationEventHandler<OrderCancelledIntegrationEvent>
{
    public Task HandleAsync(OrderCancelledIntegrationEvent @event, CancellationToken ct) =>
        dispatcher.Send(new CreateNotificationCommand(
            RecipientId: @event.CustomerId,
            Title: "Đơn hàng đã hủy",
            Body: $"Đơn {@event.OrderNumber} đã bị hủy. Lý do: {@event.Reason}",
            Severity: NotificationSeverity.Warning,
            Link: $"/orders/{@event.OrderId}",
            Metadata: new Dictionary<string, string>
            {
                ["orderId"] = @event.OrderId.ToString(),
                ["reason"] = @event.Reason,
                ["event"] = @event.EventType
            }), ct);
}

public sealed class PaymentSucceededNotificationHandler(IDispatcher dispatcher)
    : IIntegrationEventHandler<PaymentSucceededIntegrationEvent>
{
    public Task HandleAsync(PaymentSucceededIntegrationEvent @event, CancellationToken ct) =>
        dispatcher.Send(new CreateNotificationCommand(
            RecipientId: @event.CustomerId,
            Title: "Thanh toán thành công",
            Body: $"Đã nhận {OrderPlacedNotificationHandler.Format(@event.Amount)} {@event.Currency}. " +
                  $"Mã giao dịch: {@event.TransactionRef}",
            Severity: NotificationSeverity.Success,
            Link: $"/orders/{@event.OrderId}",
            Metadata: new Dictionary<string, string>
            {
                ["paymentId"] = @event.PaymentId.ToString(),
                ["event"] = @event.EventType
            }), ct);
}

public sealed class PaymentFailedNotificationHandler(IDispatcher dispatcher)
    : IIntegrationEventHandler<PaymentFailedIntegrationEvent>
{
    public Task HandleAsync(PaymentFailedIntegrationEvent @event, CancellationToken ct) =>
        dispatcher.Send(new CreateNotificationCommand(
            RecipientId: @event.CustomerId,
            Title: "Thanh toán thất bại",
            Body: $"Không thể thanh toán đơn hàng. {@event.Reason}",
            Severity: NotificationSeverity.Error,
            Link: $"/orders/{@event.OrderId}",
            Metadata: new Dictionary<string, string>
            {
                ["paymentId"] = @event.PaymentId.ToString(),
                ["event"] = @event.EventType
            }), ct);
}

/// <summary>Cảnh báo tồn kho thấp gửi cho nhóm quản trị, không gửi cho khách.</summary>
public sealed class LowStockNotificationHandler(IDispatcher dispatcher)
    : IIntegrationEventHandler<LowStockDetectedIntegrationEvent>
{
    public Task HandleAsync(LowStockDetectedIntegrationEvent @event, CancellationToken ct) =>
        dispatcher.Send(new CreateNotificationCommand(
            RecipientId: null,
            Title: "Cảnh báo tồn kho thấp",
            Body: $"Sản phẩm {@event.Sku} chỉ còn {@event.QuantityAvailable} " +
                  $"(ngưỡng cảnh báo: {@event.ReorderLevel}).",
            Severity: NotificationSeverity.Warning,
            Link: $"/inventory?search={@event.Sku}",
            Metadata: new Dictionary<string, string>
            {
                ["productId"] = @event.ProductId.ToString(),
                ["sku"] = @event.Sku,
                ["event"] = @event.EventType
            },
            TargetRole: "Manager"), ct);
}

/// <summary>Cho phép service bất kỳ yêu cầu gửi thông báo tùy ý qua Kafka.</summary>
public sealed class NotificationRequestedHandler(IDispatcher dispatcher)
    : IIntegrationEventHandler<NotificationRequestedIntegrationEvent>
{
    public Task HandleAsync(NotificationRequestedIntegrationEvent @event, CancellationToken ct) =>
        dispatcher.Send(new CreateNotificationCommand(
            RecipientId: @event.RecipientId,
            Title: @event.Title,
            Body: @event.Body,
            Severity: Enum.TryParse<NotificationSeverity>(@event.Severity, true, out var s)
                ? s
                : NotificationSeverity.Info,
            Link: @event.Link,
            Metadata: @event.Metadata), ct);
}
