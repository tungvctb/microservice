namespace BuildingBlocks.Infrastructure.Kafka;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>Consumer group — mỗi service 1 group để nhận full bản sao của stream.</summary>
    public string ConsumerGroupId { get; set; } = "default-group";

    /// <summary>Các topic service này subscribe.</summary>
    public string[] Topics { get; set; } = Array.Empty<string>();

    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>Tự tạo topic khi khởi động (chỉ nên bật ở môi trường dev).</summary>
    public bool AutoCreateTopics { get; set; } = true;
    public int TopicPartitions { get; set; } = 3;
    public short TopicReplicationFactor { get; set; } = 1;

    public string SecurityProtocol { get; set; } = "Plaintext";
    public string? SaslUsername { get; set; }
    public string? SaslPassword { get; set; }
}
