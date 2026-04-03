using EquiLink.Domain.Aggregates.Fund;
using MediatR;

namespace EquiLink.Api.Features.Funds.Commands;

public record CreateFundCommand(
    string Name,
    string ManagerName,
    string RiskLimitTemplateName,
    decimal MaxOrderSize,
    decimal DailyLossLimit,
    decimal ConcentrationLimit
) : IRequest<CreateFundResult>;

public record CreateFundResult(Guid FundId, string Name, FundStatus Status);
