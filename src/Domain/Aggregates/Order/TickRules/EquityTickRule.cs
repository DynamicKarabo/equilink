using EquiLink.Domain.Aggregates.Order.AssetClasses;
using EquiLink.Shared.AssetClasses;

namespace EquiLink.Domain.Aggregates.Order.TickRules;

public class EquityTickRule : ITickRule
{
    public AssetClass AssetClass => AssetClass.Equity;

    public bool Validate(decimal price, decimal tickSize)
    {
        if (price <= 0) return false;

        if (price < 1.00m)
        {
            return true;
        }

        var remainder = price % tickSize;
        return remainder == 0 || Math.Abs(remainder - tickSize) < 0.0001m;
    }
}
