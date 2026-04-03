namespace EquiLink.Shared.Risk;

public record RiskRuleResult(
    bool Passed,
    string? FailureReason
)
{
    public static RiskRuleResult Pass() => new(true, null);
    public static RiskRuleResult Fail(string reason) => new(false, reason);
}
