using EquiLink.Infrastructure.ReadModels;
using EquiLink.Infrastructure.ReadRepositories;
using EquiLink.Infrastructure.Tenancy;
using MediatR;

namespace EquiLink.Api.Features.Orders.Queries;

public class GetOrderHandler(IOrderReadRepository readRepository, ICurrentFundContext fundContext)
    : IRequestHandler<GetOrderQuery, OrderSummaryProjection?>
{
    public async Task<OrderSummaryProjection?> Handle(
        GetOrderQuery request,
        CancellationToken cancellationToken)
    {
        var fundId = fundContext.FundId ?? Guid.Empty;
        return await readRepository.GetByIdAsync(request.OrderId, fundId, cancellationToken);
    }
}
