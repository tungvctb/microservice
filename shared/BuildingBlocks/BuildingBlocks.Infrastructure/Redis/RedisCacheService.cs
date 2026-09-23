using System.Text.Json;
using BuildingBlocks.Application.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace BuildingBlocks.Infrastructure.Redis;

/// <summary>
/// Cache-aside trên Redis. Lỗi Redis không làm gãy request — chỉ log rồi đi thẳng xuống DB.
/// </summary>
public sealed class RedisCacheService(
    IConnectionMultiplexer redis,
    IOptions<RedisOptions> options,
    ILogger<RedisCacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RedisOptions _options = options.Value;
    private readonly IDatabase _db = redis.GetDatabase();

    private string Key(string key) => $"{_options.InstanceName}:{key}";

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var value = await _db.StringGetAsync(Key(key));
            return value.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>(value!, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Đọc cache {Key} lỗi — fallback xuống nguồn dữ liệu", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        try
        {
            var payload = JsonSerializer.Serialize(value, JsonOptions);
            await _db.StringSetAsync(Key(key), payload, ttl ?? TimeSpan.FromMinutes(_options.DefaultTtlMinutes));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ghi cache {Key} lỗi", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try { await _db.KeyDeleteAsync(Key(key)); }
        catch (Exception ex) { logger.LogWarning(ex, "Xóa cache {Key} lỗi", key); }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        try
        {
            var pattern = $"{Key(prefix)}*";
            foreach (var endpoint in redis.GetEndPoints())
            {
                var server = redis.GetServer(endpoint);
                if (!server.IsConnected || server.IsReplica) continue;

                // SCAN thay vì KEYS: không chặn Redis khi keyspace lớn.
                await foreach (var key in server.KeysAsync(pattern: pattern, pageSize: 250).WithCancellation(ct))
                    await _db.KeyDeleteAsync(key);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Xóa cache theo prefix {Prefix} lỗi", prefix);
        }
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory,
        TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null) return cached;

        var value = await factory(ct);
        if (value is not null) await SetAsync(key, value, ttl, ct);
        return value;
    }
}
