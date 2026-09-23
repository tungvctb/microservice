namespace BuildingBlocks.Application.Caching;

/// <summary>Trừu tượng cache phân tán (Redis) cho tầng Application.</summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);

    /// <summary>Cache-aside: trả cache nếu có, không thì gọi factory rồi ghi cache.</summary>
    Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? ttl = null, CancellationToken ct = default);
}

/// <summary>Khóa phân tán dựa trên Redis — dùng khi nhiều instance cùng ghi 1 aggregate.</summary>
public interface IDistributedLock
{
    Task<IAsyncDisposable?> AcquireAsync(string resource, TimeSpan expiry, TimeSpan wait, CancellationToken ct = default);
}
