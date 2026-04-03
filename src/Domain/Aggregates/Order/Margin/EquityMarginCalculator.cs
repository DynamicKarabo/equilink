using EquiLink.Domain.Aggregates.Order.AssetClasses;
using EquiLink.Shared.AssetClasses;

namespace EquiLink.Domain.Aggregates.Order.Margin;

public class EquityMarginCalculator : IMarginCalculator
{
    public AssetClass AssetClass => AssetClass.Equity;

    public decimal CalculateInitialMargin(decimal quantity, decimal price, AssetClassConfiguration configuration)
    {
        var notional = quantity * price;
        return notional * configuration.InitialMarginRequirement;
    }

    public decimal CalculateMaintenanceMargin(decimal quantity, decimal price, AssetClassConfiguration configuration)
    {
        var notional = quantity * price;
        return notional * configuration.MaintenanceMarginRequirement;
    }
}
