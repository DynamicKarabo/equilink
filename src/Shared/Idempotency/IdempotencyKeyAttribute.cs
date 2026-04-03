namespace EquiLink.Shared.Idempotency;

[AttributeUsage(AttributeTargets.Class)]
public sealed class IdempotencyKeyAttribute : Attribute
{
    public int TtlHours { get; }
    public string FundIdPropertyName { get; }

    public IdempotencyKeyAttribute(int ttlHours = 24, string fundIdPropertyName = "FundId")
    {
        TtlHours = ttlHours;
        FundIdPropertyName = fundIdPropertyName;
    }
}
