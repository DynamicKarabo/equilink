using System.Text.Json;
using EquiLink.Domain.Events;
using StackExchange.Redis;

namespace EquiLink.Infrastructure.Snapshots;

public interface ISnapshotStore
{
    Task SaveSnapshotAsync<T>(Guid aggregateId, int version, T state, CancellationToken ct = default) where T : class;
    Task<T?> LoadSnapshotAsync<T>(Guid aggregateId, CancellationToken ct = default) where T : class;
}

public class RedisSnapshotStore : ISnapshotStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly JsonSerializerOptions _jsonOptions;

    public RedisSnapshotStore(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task SaveSnapshotAsync<T>(Guid aggregateId, int version, T state, CancellationToken ct = default) where T : class
    {
        var db = _redis.GetDatabase();
        var key = $"snapshot:{aggregateId}";
        var snapshot = new SnapshotEntry
        {
            AggregateId = aggregateId,
            Version = version,
            State = state,
            SavedAt = DateTimeOffset.UtcNow
        };
        
        var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
        await db.StringSetAsync(key, json, TimeSpan.FromDays(7));
    }

    public async Task<T?> LoadSnapshotAsync<T>(Guid aggregateId, CancellationToken ct = default) where T : class
    {
        var db = _redis.GetDatabase();
        var key = $"snapshot:{aggregateId}";
        
        var value = await db.StringGetAsync(key);
        
        if (!value.HasValue)
        {
            return null;
        }
        
        var snapshot = JsonSerializer.Deserialize<SnapshotEntry>(value!, _jsonOptions);
        return snapshot?.State as T;
    }
}

public class SnapshotEntry
{
    public Guid AggregateId { get; set; }
    public int Version { get; set; }
    public object? State { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}
