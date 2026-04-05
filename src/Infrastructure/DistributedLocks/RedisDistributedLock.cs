using System.Text.Json;
using EquiLink.Shared.Risk;
using StackExchange.Redis;

namespace EquiLink.Infrastructure.DistributedLocks;

public interface IDistributedLock
{
    Task<bool> TryAcquireAsync(string resource, TimeSpan timeout, CancellationToken ct = default);
    Task ReleaseAsync(string resource);
}

public class RedisDistributedLock : IDistributedLock
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;

    public RedisDistributedLock(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _db = redis.GetDatabase();
    }

    public async Task<bool> TryAcquireAsync(string resource, TimeSpan timeout, CancellationToken ct = default)
    {
        var lockKey = $"lock:{resource}";
        var lockValue = Guid.NewGuid().ToString();

        var acquired = await _db.StringSetAsync(
            lockKey,
            lockValue,
            timeout,
            When.NotExists
        );

        return acquired;
    }

    public async Task ReleaseAsync(string resource)
    {
        var lockKey = $"lock:{resource}";
        await _db.KeyDeleteAsync(lockKey);
    }
}

public class OptimisticRiskStateCache : IRiskStateCache
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly JsonSerializerOptions _jsonOptions = new();

    public OptimisticRiskStateCache(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _db = redis.GetDatabase();
    }

    public async Task<HashSet<string>> GetBlacklistedSymbolsAsync(string fundId, CancellationToken cancellationToken = default)
    {
        var key = $"risk:{fundId}:blacklist";
        var value = await _db.StringGetAsync(key);
        
        if (!value.HasValue)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
        return JsonSerializer.Deserialize<HashSet<string>>(value, _jsonOptions) 
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<decimal?> GetMaxOrderSizeAsync(string fundId, CancellationToken cancellationToken = default)
    {
        var key = $"risk:{fundId}:max_order_size";
        var value = await _db.StringGetAsync(key);
        
        if (!value.HasValue)
            return null;
            
        return decimal.TryParse(value, out var result) ? result : null;
    }

    public async Task<decimal?> GetCurrentExposureAsync(string fundId, CancellationToken cancellationToken = default)
    {
        var key = $"risk:{fundId}:current_exposure";
        var value = await _db.StringGetAsync(key);
        
        if (!value.HasValue)
            return null;
            
        return decimal.TryParse(value, out var result) ? result : null;
    }
}
