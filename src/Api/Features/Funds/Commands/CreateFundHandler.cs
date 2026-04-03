using EquiLink.Domain.Aggregates.Fund;
using EquiLink.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EquiLink.Api.Features.Funds.Commands;

public class CreateFundHandler(EquiLinkDbContext dbContext)
    : IRequestHandler<CreateFundCommand, CreateFundResult>
{
    public async Task<CreateFundResult> Handle(
        CreateFundCommand request,
        CancellationToken cancellationToken)
    {
        var existingTemplate = await dbContext.FundRiskLimitTemplates
            .FirstOrDefaultAsync(t => t.TemplateName == request.RiskLimitTemplateName, cancellationToken);

        FundRiskLimitTemplate template;

        if (existingTemplate != null)
        {
            template = existingTemplate;
        }
        else
        {
            template = FundRiskLimitTemplate.Create(
                request.RiskLimitTemplateName,
                request.MaxOrderSize,
                request.DailyLossLimit,
                request.ConcentrationLimit);

            dbContext.FundRiskLimitTemplates.Add(template);
        }

        var fund = Fund.Create(
            Guid.NewGuid(),
            request.Name,
            request.ManagerName,
            template);

        dbContext.Funds.Add(fund);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateFundResult(fund.Id, fund.Name, fund.Status);
    }
}
