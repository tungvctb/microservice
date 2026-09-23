namespace BuildingBlocks.Infrastructure.Redis;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";
    public string ConnectionString { get; set; } = "localhost:6379";
    /// <summary>Tiền tố khóa, tách namespace giữa các service dùng chung 1 Redis.</summary>
    public string InstanceName { get; set; } = "ecommerce";
    public int DefaultTtlMinutes { get; set; } = 10;
}
