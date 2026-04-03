using EquiLink.Domain.Aggregates.Order.AssetClasses;
using EquiLink.Shared.AssetClasses;

namespace EquiLink.Domain.Aggregates.Order.Margin;

public interface IMarginCalculator
{
    AssetClass AssetClass { get; }
    decimal CalculateInitialMargin(decimal quantity, decimal price, AssetClassConfiguration configuration);
    decimal CalculateMaintenanceMargin(decimal quantity, decimal price, AssetClassConfiguration configuration);
}
