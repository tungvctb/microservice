using System.Text;
using System.Text.Json;
using BuildingBlocks.Contracts.IntegrationEvents;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.Kafka;

/// <summary>
/// Vòng lặp consume Kafka chạy nền.
/// Luồng: nhận message → dedupe qua Inbox → dispatch handler → retry → DLQ nếu vẫn lỗi → commit offset.
/// Offset chỉ được store sau khi xử lý xong, nên crash giữa chừng sẽ consume lại (at-least-once).
/// </summary>
public sealed class KafkaConsumerService(
    IOptions<KafkaOptions> options,
    IServiceScopeFactory scopeFactory,
    IntegrationEventRegistry registry,
    ILogger<KafkaConsumerService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly KafkaOptions _options = options.Value;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.Topics.Length == 0)
        {
            logger.LogInformation("Không cấu hình topic nào — consumer không khởi động.");
            return Task.CompletedTask;
        }

        // Chạy trên thread riêng vì Consume() là blocking call.
        return Task.Factory.StartNew(() => ConsumeLoop(stoppingToken),
            stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
    }

    private async Task ConsumeLoop(CancellationToken ct)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            // Tự commit theo chu kỳ, nhưng offset chỉ được "store" khi xử lý thành công.
            EnableAutoOffsetStore = false,
            SessionTimeoutMs = 45_000,
            MaxPollIntervalMs = 300_000
        };
        KafkaProducer.ApplySecurity(config, _options);

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => logger.LogError("Kafka consumer lỗi: {Reason}", e.Reason))
            .SetPartitionsAssignedHandler((_, parts) =>
                logger.LogInformation("Được gán partition: {Partitions}", string.Join(", ", parts)))
            .Build();

        consumer.Subscribe(_options.Topics);
        logger.LogInformation("Consumer group {Group} đang lắng nghe {Topics}",
            _options.ConsumerGroupId, string.Join(", ", _options.Topics));

        while (!ct.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;
            try
            {
                result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result?.Message is null) continue;

                await ProcessWithRetryAsync(result, ct);
                consumer.StoreOffset(result);
            }
            catch (OperationCanceledException) { break; }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Lỗi khi consume: {Reason}", ex.Error.Reason);
                await Task.Delay(1000, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi không mong đợi khi xử lý message");
                if (result is not null) consumer.StoreOffset(result);
            }
        }

        consumer.Close();
    }

    private async Task ProcessWithRetryAsync(ConsumeResult<string, string> result, CancellationToken ct)
    {
        var eventTypeName = GetHeader(result, KafkaProducer.EventTypeHeader);
        if (string.IsNullOrEmpty(eventTypeName))
        {
            logger.LogWarning("Message thiếu header {Header}, bỏ qua", KafkaProducer.EventTypeHeader);
            return;
        }

        var clrType = registry.Resolve(eventTypeName);
        if (clrType is null)
        {
            logger.LogDebug("EventType {EventType} không thuộc quan tâm của service này", eventTypeName);
            return;
        }

        if (JsonSerializer.Deserialize(result.Message.Value, clrType, JsonOptions) is not IntegrationEvent @event)
        {
            logger.LogWarning("Không deserialize được {EventType}", eventTypeName);
            return;
        }

        for (var attempt = 1; attempt <= _options.MaxRetryAttempts; attempt++)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sp = scope.ServiceProvider;

                var inbox = sp.GetService<IInboxStore>();
                if (inbox is not null && await inbox.HasProcessedAsync(@event.EventId, _options.ConsumerGroupId, ct))
                {
                    logger.LogDebug("Event {EventId} đã xử lý trước đó — bỏ qua", @event.EventId);
                    return;
                }

                var handled = await sp.GetRequiredService<IntegrationEventDispatcher>()
                    .DispatchAsync(@event, sp, ct);

                if (handled > 0 && inbox is not null)
                    await inbox.MarkProcessedAsync(@event.EventId, _options.ConsumerGroupId, eventTypeName, ct);

                if (handled > 0)
                    logger.LogInformation("Đã xử lý {EventType} ({EventId}) bởi {Count} handler",
                        eventTypeName, @event.EventId, handled);
                return;
            }
            catch (Exception ex) when (attempt < _options.MaxRetryAttempts)
            {
                logger.LogWarning(ex, "Xử lý {EventType} lỗi lần {Attempt}/{Max}, thử lại...",
                    eventTypeName, attempt, _options.MaxRetryAttempts);
                await Task.Delay(_options.RetryDelayMs * attempt, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Xử lý {EventType} thất bại sau {Max} lần — đẩy sang DLQ",
                    eventTypeName, _options.MaxRetryAttempts);
                await SendToDeadLetterAsync(result, eventTypeName, ex, ct);
                return;
            }
        }
    }

    private async Task SendToDeadLetterAsync(ConsumeResult<string, string> result, string eventType,
        Exception error, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            await bus.PublishRawAsync(
                KafkaTopics.DeadLetterFor(result.Topic),
                result.Message.Key,
                result.Message.Value,
                new Dictionary<string, string>
                {
                    [KafkaProducer.EventTypeHeader] = eventType,
                    ["dlq-reason"] = error.Message,
                    ["dlq-source-topic"] = result.Topic,
                    ["dlq-timestamp"] = DateTime.UtcNow.ToString("O")
                }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không đẩy được message sang DLQ");
        }
    }

    private static string? GetHeader(ConsumeResult<string, string> result, string name)
    {
        if (result.Message.Headers is null) return null;
        return result.Message.Headers.TryGetLastBytes(name, out var bytes)
            ? Encoding.UTF8.GetString(bytes)
            : null;
    }
}
