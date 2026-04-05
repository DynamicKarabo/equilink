namespace EquiLink.Shared.Idempotency;

[AttributeUsage(AttributeTargets.Class)]
public sealed class IdempotencyKeyAttribute : Attribute
{
    public string FundIdPropertyName { get; }
    public string IdempotencyKeyPropertyName { get; }
    public int TtlHours { get; }

    public IdempotencyKeyAttribute(string fundIdPropertyName = "FundId", string idempotencyKeyPropertyName = "IdempotencyKey", int ttlHours = 24)
    {
        FundIdPropertyName = fundIdPropertyName;
        IdempotencyKeyPropertyName = idempotencyKeyPropertyName;
        TtlHours = ttlHours;
    }
}
