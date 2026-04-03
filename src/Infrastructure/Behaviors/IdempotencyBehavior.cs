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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerOptions.Default);

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

            var cachedResponse = JsonSerializer.Deserialize<TResponse>(cachedValue!, JsonOptions);

            if (cachedResponse != null)
            {
                return cachedResponse;
            }
        }

        var response = await next();

        var serialized = JsonSerializer.Serialize(response, JsonOptions);

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
}
