namespace BuildingBlocks.Contracts.IntegrationEvents;

/// <summary>Danh mục topic Kafka. Một topic cho mỗi bounded context (event stream của context đó).</summary>
public static class KafkaTopics
{
    public const string ProductEvents   = "ecommerce.product.events";
    public const string InventoryEvents = "ecommerce.inventory.events";
    public const string OrderEvents     = "ecommerce.order.events";
    public const string PaymentEvents   = "ecommerce.payment.events";
    public const string NotificationCommands = "ecommerce.notification.commands";

    /// <summary>Dead-letter topic: message consume lỗi quá số lần retry sẽ được đẩy sang đây.</summary>
    public static string DeadLetterFor(string topic) => $"{topic}.dlq";

    public static readonly IReadOnlyList<string> All = new[]
    {
        ProductEvents, InventoryEvents, OrderEvents, PaymentEvents, NotificationCommands
    };
}
