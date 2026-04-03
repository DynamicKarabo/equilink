using System.Text.Json;
using EquiLink.Shared.Risk;
using StackExchange.Redis;

namespace EquiLink.Infrastructure.RiskEngine;

public class RedisRiskStateCache(IConnectionMultiplexer redis) : IRiskStateCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerOptions.Default);

    public async Task<HashSet<string>> GetBlacklistedSymbolsAsync(
        string fundId, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var key = $"risk:{fundId}:blacklist";
        var value = await db.StringGetAsync(key);

        if (!value.HasValue)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return JsonSerializer.Deserialize<HashSet<string>>(value!, JsonOptions)
               ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<decimal?> GetMaxOrderSizeAsync(
        string fundId, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var key = $"risk:{fundId}:max_order_size";
        var value = await db.StringGetAsync(key);

        if (!value.HasValue)
        {
            return null;
        }

        return decimal.TryParse(value!, out var result) ? result : null;
    }

    public async Task<decimal?> GetCurrentExposureAsync(
        string fundId, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var key = $"risk:{fundId}:current_exposure";
        var value = await db.StringGetAsync(key);

        if (!value.HasValue)
        {
            return null;
        }

        return decimal.TryParse(value!, out var result) ? result : null;
    }
}
