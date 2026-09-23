using BuildingBlocks.Infrastructure.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";
    public int PollingIntervalSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 50;
    public int MaxRetryCount { get; set; } = 5;
}

/// <summary>
/// Quét bảng Outbox theo chu kỳ và publish lên Kafka. Message publish lỗi được retry
/// tới MaxRetryCount rồi đánh dấu thất bại để con người xử lý.
/// </summary>
public sealed class OutboxProcessor<TContext>(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor<TContext>> logger) : BackgroundService
    where TContext : DbContext, IIntegrationDbContext
{
    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_options.PollingIntervalSeconds);
        logger.LogInformation("Outbox processor khởi động (mỗi {Seconds}s, batch {Batch})",
            _options.PollingIntervalSeconds, _options.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox processor gặp lỗi ở vòng lặp này");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task PublishPendingAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null && m.RetryCount < _options.MaxRetryCount)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        foreach (var message in pending)
        {
            try
            {
                var headers = new Dictionary<string, string> { [KafkaProducer.EventTypeHeader] = message.EventType };
                if (message.CorrelationId is not null)
                    headers[KafkaProducer.CorrelationHeader] = message.CorrelationId;

                await bus.PublishRawAsync(message.Topic, message.AggregateId, message.Payload, headers, ct);

                message.ProcessedOnUtc = DateTime.UtcNow;
                message.Error = null;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.Error = ex.Message;
                logger.LogWarning(ex, "Outbox message {EventType} ({EventId}) publish lỗi lần {Retry}",
                    message.EventType, message.EventId, message.RetryCount);
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogDebug("Outbox: xử lý {Count} message", pending.Count);
    }
}
