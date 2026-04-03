using EquiLink.Shared.AssetClasses;

namespace EquiLink.Domain.Aggregates.Order.TickRules;

public class FutureTickRule : ITickRule
{
    public AssetClass AssetClass => AssetClass.Future;

    public bool Validate(decimal price, decimal tickSize)
    {
        if (price <= 0) return false;

        var remainder = price % tickSize;
        return remainder == 0 || Math.Abs(remainder - tickSize) < 0.0001m;
    }
}
