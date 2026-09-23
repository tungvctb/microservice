using System.Text;
using System.Text.Json;
using BuildingBlocks.Contracts.IntegrationEvents;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.Kafka;

public sealed class KafkaProducer : IEventBus, IDisposable
{
    public const string EventTypeHeader = "event-type";
    public const string CorrelationHeader = "correlation-id";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;

    public KafkaProducer(IOptions<KafkaOptions> options, ILogger<KafkaProducer> logger)
    {
        _logger = logger;
        var cfg = options.Value;

        var config = new ProducerConfig
        {
            BootstrapServers = cfg.BootstrapServers,
            // acks=all + idempotence: không mất message, không ghi trùng khi producer retry.
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = 5,
            RetryBackoffMs = 200,
            LingerMs = 5,
            CompressionType = CompressionType.Snappy
        };

        ApplySecurity(config, cfg);

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => _logger.LogError("Kafka producer lỗi: {Reason}", e.Reason))
            .Build();
    }

    internal static void ApplySecurity(ClientConfig config, KafkaOptions cfg)
    {
        if (string.Equals(cfg.SecurityProtocol, "Plaintext", StringComparison.OrdinalIgnoreCase)) return;

        config.SecurityProtocol = Enum.Parse<SecurityProtocol>(cfg.SecurityProtocol, ignoreCase: true);
        if (string.IsNullOrEmpty(cfg.SaslUsername)) return;

        config.SaslMechanism = SaslMechanism.Plain;
        config.SaslUsername = cfg.SaslUsername;
        config.SaslPassword = cfg.SaslPassword;
    }

    public Task PublishAsync<TEvent>(TEvent @event, string topic, CancellationToken ct = default)
        where TEvent : IntegrationEvent
    {
        var payload = JsonSerializer.Serialize(@event, @event.GetType(), JsonOptions);
        var headers = new Dictionary<string, string> { [EventTypeHeader] = @event.EventType };
        if (@event.CorrelationId is not null) headers[CorrelationHeader] = @event.CorrelationId;

        return PublishRawAsync(topic, @event.AggregateId, payload, headers, ct);
    }

    public async Task PublishRawAsync(string topic, string key, string payload,
        IDictionary<string, string>? headers = null, CancellationToken ct = default)
    {
        var message = new Message<string, string> { Key = key, Value = payload, Headers = new Headers() };

        foreach (var (name, value) in headers ?? new Dictionary<string, string>())
            message.Headers.Add(name, Encoding.UTF8.GetBytes(value));

        try
        {
            var result = await _producer.ProduceAsync(topic, message, ct);
            _logger.LogDebug("Đã publish lên {Topic} partition {Partition} offset {Offset} (key={Key})",
                topic, result.Partition.Value, result.Offset.Value, key);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Publish lên {Topic} thất bại (key={Key})", topic, key);
            throw;
        }
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
