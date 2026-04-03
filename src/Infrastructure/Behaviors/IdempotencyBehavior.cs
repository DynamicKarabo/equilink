using System.Reflection;
using System.Text.Json;
using EquiLink.Shared.Idempotency;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace EquiLink.Infrastructure.Behaviors;

public class IdempotencyBehavior<TRequest, TResponse>(
    IConnectionMultiplexer redis,
    IHttpContextAccessor httpContextAccessor,
    ILogger<IdempotencyBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const string IdempotencyKeyHeader = "X-Idempotency-Key";
    private const string IdempotencyKeyProperty = "IdempotencyKey";

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var attribute = typeof(TRequest).GetCustomAttribute<IdempotencyKeyAttribute>();

        if (attribute == null)
        {
            return await next();
        }

        var idempotencyKey = ExtractIdempotencyKey(request);

        if (string.IsNullOrEmpty(idempotencyKey))
        {
            throw new InvalidOperationException("Idempotency key is required for requests marked with [IdempotencyKey].");
        }

        var fundId = ExtractFundId(request, attribute.FundIdPropertyName);
        var redisKey = $"idempotency:{fundId}:{idempotencyKey}";

        var db = redis.GetDatabase();

        var cachedValue = await db.StringGetAsync(redisKey);

        if (cachedValue.HasValue)
        {
            logger.LogDebug("Idempotent retry detected for key {RedisKey}", redisKey);

            var cached = JsonSerializer.Deserialize<CachedResponse>(cachedValue!);

            if (cached != null)
            {
                return (TResponse)cached.Response!;
            }
        }

        var response = await next();

        var cacheEntry = new CachedResponse(
            Response: response,
            OriginalOrderId: ExtractOrderId(response),
            StatusCode: 200,
            Timestamp: DateTimeOffset.UtcNow
        );

        var serialized = JsonSerializer.Serialize(cacheEntry);

        await db.StringSetAsync(
            redisKey,
            serialized,
            TimeSpan.FromHours(attribute.TtlHours),
            When.NotExists
        );

        logger.LogInformation("Cached idempotent response for key {RedisKey}", redisKey);

        return response;
    }

    private string ExtractIdempotencyKey(TRequest request)
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext != null && httpContext.Request.Headers.TryGetValue(IdempotencyKeyHeader, out var headerValue))
        {
            var key = headerValue.ToString();
            if (!string.IsNullOrEmpty(key))
            {
                return key;
            }
        }

        var prop = typeof(TRequest).GetProperty(IdempotencyKeyProperty);

        if (prop != null)
        {
            return prop.GetValue(request)?.ToString() ?? string.Empty;
        }

        return string.Empty;
    }

    private string ExtractFundId(TRequest request, string propertyName)
    {
        var prop = typeof(TRequest).GetProperty(propertyName);

        if (prop != null)
        {
            var value = prop.GetValue(request);
            return value?.ToString() ?? "unknown";
        }

        return "unknown";
    }

    private string? ExtractOrderId(TResponse response)
    {
        var prop = typeof(TResponse).GetProperty("OrderId");
        return prop?.GetValue(response)?.ToString();
    }

    private record CachedResponse(object? Response, string? OriginalOrderId, int StatusCode, DateTimeOffset Timestamp);
}
