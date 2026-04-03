using EquiLink.Shared.AssetClasses;

namespace EquiLink.Domain.Aggregates.Order.TickRules;

public interface ITickRule
{
    AssetClass AssetClass { get; }
    bool Validate(decimal price, decimal tickSize);
}
