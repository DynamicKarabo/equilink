namespace EquiLink.Shared.Risk;

public interface IRiskRule
{
    int Order { get; }
    string Name { get; }
    Task<RiskRuleResult> EvaluateAsync(object request, CancellationToken cancellationToken);
}
