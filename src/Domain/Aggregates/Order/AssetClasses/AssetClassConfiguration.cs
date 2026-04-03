using EquiLink.Shared.AssetClasses;

namespace EquiLink.Domain.Aggregates.Order.AssetClasses;

public record AssetClassConfiguration(
    AssetClass AssetClass,
    decimal TickSize,
    decimal LotSize,
    decimal InitialMarginRequirement,
    decimal MaintenanceMarginRequirement,
    bool ExtendedHoursAllowed
);
