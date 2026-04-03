namespace EquiLink.Infrastructure.RiskEngine;

public class RiskRuleViolationException(string ruleName, string? reason)
    : InvalidOperationException($"Risk rule '{ruleName}' failed: {reason}")
{
    public string RuleName { get; } = ruleName;
    public string? Reason { get; } = reason;
}
