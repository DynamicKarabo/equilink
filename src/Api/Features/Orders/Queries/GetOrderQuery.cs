using EquiLink.Infrastructure.ReadModels;
using MediatR;

namespace EquiLink.Api.Features.Orders.Queries;

public record GetOrderQuery(Guid OrderId) : IRequest<OrderSummaryProjection?>;
