using EquiLink.Shared.Risk;
using Microsoft.Extensions.Logging;

namespace EquiLink.Infrastructure.RiskEngine;

public class MaxOrderSizeRule(
    IRiskStateCache riskStateCache,
    ILogger<MaxOrderSizeRule> logger) : IRiskRule
{
    public int Order => 2;
    public string Name => "MaxOrderSize";

    public async Task<RiskRuleResult> EvaluateAsync(object request, CancellationToken cancellationToken)
    {
        if (request is not IOrderRequest orderRequest)
        {
            return RiskRuleResult.Pass();
        }

        var maxOrderSize = await riskStateCache.GetMaxOrderSizeAsync(
            orderRequest.FundId.ToString(), cancellationToken);

        if (maxOrderSize.HasValue && orderRequest.Quantity > maxOrderSize.Value)
        {
            logger.LogWarning(
                "Order rejected: quantity {Quantity} exceeds max order size {MaxOrderSize} for fund {FundId}",
                orderRequest.Quantity, maxOrderSize.Value, orderRequest.FundId);

            return RiskRuleResult.Fail(
                $"Order quantity {orderRequest.Quantity} exceeds maximum allowed size of {maxOrderSize.Value}.");
        }

        return RiskRuleResult.Pass();
    }
}
