namespace EquiLink.Shared.Idempotency;

public interface IIdempotencyResult
{
    bool IsCached { get; }
    Guid? OriginalOrderId { get; }
}
