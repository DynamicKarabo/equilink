using EquiLink.Shared.AssetClasses;
using EquiLink.Shared.Idempotency;
using EquiLink.Shared.Risk;
using MediatR;

namespace EquiLink.Api.Features.Orders.Commands;

[IdempotencyKey]
public record CreateOrderCommand(
    Guid FundId,
    string Symbol,
    string Side,
    decimal Quantity,
    decimal? LimitPrice,
    AssetClass AssetClass,
    string IdempotencyKey
) : IRequest<CreateOrderResult>, IOrderRequest;

public record CreateOrderResult(Guid OrderId);
