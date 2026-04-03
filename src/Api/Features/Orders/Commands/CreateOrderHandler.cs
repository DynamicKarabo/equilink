using EquiLink.Domain.Aggregates.Order;
using EquiLink.Domain.EventStore;
using MediatR;

namespace EquiLink.Api.Features.Orders.Commands;

public class CreateOrderHandler(IEventStore eventStore)
    : IRequestHandler<CreateOrderCommand, CreateOrderResult>
{
    public Task<CreateOrderResult> Handle(
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

        eventStore.AppendAsync(order.Id, events, cancellationToken);

        return Task.FromResult(new CreateOrderResult(order.Id));
    }
}
