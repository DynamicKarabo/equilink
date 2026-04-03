namespace EquiLink.Shared.Idempotency;

public record IdempotencyResult(
    bool IsCached,
    object? CachedResponse,
    string? OriginalOrderId
);
