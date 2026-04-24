using EquiLink.Domain.Aggregates.Order;
using EquiLink.Domain.EventStore;
using MediatR;

namespace EquiLink.Api.Features.Orders.Commands;

public class CreateOrderHandler(IEventStore eventStore)
    : IRequestHandler<CreateOrderCommand, CreateOrderResult>
{
    public async Task<CreateOrderResult> Handle(
        CreateOrderCommand request,
        CancellationToken cancellationToken)
    {
        var order = OrderAggregate.Create(
            request.FundId,
            request.Symbol,
            request.Side,
            request.Quantity,
            request.LimitPrice,
            request.AssetClass);

        var events = order.DequeueUncommittedEvents();

        await eventStore.AppendAsync(order.Id, events, cancellationToken);

        return new CreateOrderResult(order.Id);
    }
}
