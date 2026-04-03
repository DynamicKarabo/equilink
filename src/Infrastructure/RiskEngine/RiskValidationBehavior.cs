using EquiLink.Shared.Risk;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EquiLink.Infrastructure.RiskEngine;

public class RiskValidationBehavior<TRequest, TResponse>(
    IEnumerable<IRiskRule> riskRules,
    ILogger<RiskValidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var orderedRules = riskRules.OrderBy(r => r.Order).ToList();

        foreach (var rule in orderedRules)
        {
            var result = await rule.EvaluateAsync(request, cancellationToken);

            if (!result.Passed)
            {
                logger.LogWarning(
                    "Risk rule {RuleName} failed: {Reason}",
                    rule.Name, result.FailureReason);

                throw new RiskRuleViolationException(rule.Name, result.FailureReason);
            }
        }

        return await next();
    }
}
