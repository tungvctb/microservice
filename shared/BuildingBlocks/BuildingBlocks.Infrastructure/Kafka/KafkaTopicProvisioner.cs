using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BuildingBlocks.Contracts.IntegrationEvents;

namespace BuildingBlocks.Infrastructure.Kafka;

/// <summary>
/// Tạo sẵn topic khi service khởi động (dev/test). Production nên quản lý topic bằng IaC.
/// Là BackgroundService chứ không phải IHostedService: host chỉ await StartAsync tới lần yield
/// đầu tiên, nên Kafka chậm hoặc chết cũng KHÔNG chặn Kestrel bắt đầu nhận request.
/// </summary>
public sealed class KafkaTopicProvisioner(
    IOptions<KafkaOptions> options,
    ILogger<KafkaTopicProvisioner> logger) : BackgroundService
{
    private readonly KafkaOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!_options.AutoCreateTopics) return;

        // Nhả luồng khởi động ngay lập tức: GetMetadata bên dưới là lời gọi blocking.
        await Task.Yield();

        var config = new AdminClientConfig { BootstrapServers = _options.BootstrapServers };
        KafkaProducer.ApplySecurity(config, _options);

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                using var admin = new AdminClientBuilder(config).Build();
                var metadata = admin.GetMetadata(TimeSpan.FromSeconds(10));
                var existing = metadata.Topics.Select(t => t.Topic).ToHashSet();

                var wanted = KafkaTopics.All
                    .Concat(KafkaTopics.All.Select(KafkaTopics.DeadLetterFor))
                    .Where(t => !existing.Contains(t))
                    .Select(t => new TopicSpecification
                    {
                        Name = t,
                        NumPartitions = _options.TopicPartitions,
                        ReplicationFactor = _options.TopicReplicationFactor
                    })
                    .ToList();

                if (wanted.Count > 0)
                {
                    await admin.CreateTopicsAsync(wanted);
                    logger.LogInformation("Đã tạo topic: {Topics}", string.Join(", ", wanted.Select(t => t.Name)));
                }
                return;
            }
            catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Chưa kết nối được Kafka (lần {Attempt}/10): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }

        logger.LogError("Không provisioning được topic sau 10 lần thử — service vẫn chạy tiếp.");
    }
}
