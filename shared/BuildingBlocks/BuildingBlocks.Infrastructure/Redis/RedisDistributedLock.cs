using BuildingBlocks.Application.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace BuildingBlocks.Infrastructure.Redis;

/// <summary>
/// Khóa phân tán kiểu SET NX PX. Giải phóng bằng Lua script so khớp token,
/// tránh trường hợp instance A xóa nhầm khóa mà instance B đang giữ.
/// </summary>
public sealed class RedisDistributedLock(
    IConnectionMultiplexer redis,
    IOptions<RedisOptions> options,
    ILogger<RedisDistributedLock> logger) : IDistributedLock
{
    private const string ReleaseScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        else
            return 0
        end
        """;

    private readonly IDatabase _db = redis.GetDatabase();
    private readonly RedisOptions _options = options.Value;

    public async Task<IAsyncDisposable?> AcquireAsync(string resource, TimeSpan expiry, TimeSpan wait,
        CancellationToken ct = default)
    {
        var key = $"{_options.InstanceName}:lock:{resource}";
        var token = Guid.NewGuid().ToString("N");
        var deadline = DateTime.UtcNow.Add(wait);

        do
        {
            if (await _db.StringSetAsync(key, token, expiry, When.NotExists))
            {
                logger.LogDebug("Đã giữ khóa {Resource}", resource);
                return new Handle(_db, key, token, logger, resource);
            }

            await Task.Delay(50, ct);
        }
        while (DateTime.UtcNow < deadline);

        logger.LogWarning("Không lấy được khóa {Resource} sau {Wait}", resource, wait);
        return null;
    }

    private sealed class Handle(IDatabase db, string key, string token, ILogger logger, string resource)
        : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await db.ScriptEvaluateAsync(ReleaseScript, new RedisKey[] { key }, new RedisValue[] { token });
                logger.LogDebug("Đã nhả khóa {Resource}", resource);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Nhả khóa {Resource} lỗi — khóa sẽ tự hết hạn", resource);
            }
        }
    }
}
