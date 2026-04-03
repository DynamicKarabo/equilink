using EquiLink.Shared.Risk;
using Microsoft.Extensions.Logging;

namespace EquiLink.Infrastructure.RiskEngine;

public class SymbolBlacklistRule(
    IRiskStateCache riskStateCache,
    ILogger<SymbolBlacklistRule> logger) : IRiskRule
{
    public int Order => 1;
    public string Name => "SymbolBlacklist";

    public async Task<RiskRuleResult> EvaluateAsync(object request, CancellationToken cancellationToken)
    {
        if (request is not IOrderRequest orderRequest)
        {
            return RiskRuleResult.Pass();
        }

        var blacklistedSymbols = await riskStateCache.GetBlacklistedSymbolsAsync(
            orderRequest.FundId.ToString(), cancellationToken);

        if (blacklistedSymbols.Contains(orderRequest.Symbol))
        {
            logger.LogWarning(
                "Order rejected: symbol {Symbol} is blacklisted for fund {FundId}",
                orderRequest.Symbol, orderRequest.FundId);

            return RiskRuleResult.Fail($"Symbol '{orderRequest.Symbol}' is blacklisted.");
        }

        return RiskRuleResult.Pass();
    }
}
