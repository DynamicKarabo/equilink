using Stateless;
using EquiLink.Domain.Aggregates.Order.Events;
using EquiLink.Domain.Events;

namespace EquiLink.Domain.Aggregates.Order;

public enum OrderState
{
    New,
    RiskValidating,
    Approved,
    Rejected,
    Submitted
}

public enum OrderTrigger
{
    StartRiskValidation,
    Approve,
    Reject,
    Submit
}

public class OrderAggregate
{
    private readonly StateMachine<OrderState, OrderTrigger> _stateMachine;
    private readonly List<IDomainEvent> _uncommittedEvents = new();
    private readonly List<IDomainEvent> _eventStream = new();

    private OrderState _currentState;

    public Guid Id { get; private set; }
    public Guid FundId { get; private set; }
    public string Symbol { get; private set; } = string.Empty;
    public string Side { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal? LimitPrice { get; private set; }
    public int Version => _eventStream.Count;

    public OrderState CurrentState => _currentState;

    public IReadOnlyList<IDomainEvent> UncommittedEvents => _uncommittedEvents;
    public IReadOnlyList<IDomainEvent> EventStream => _eventStream;

    private OrderAggregate()
    {
        _stateMachine = new StateMachine<OrderState, OrderTrigger>(
            () => _currentState,
            s => _currentState = s
        );
        ConfigureStateMachine();
    }

    public static OrderAggregate Create(Guid fundId, string symbol, string side, decimal quantity, decimal? limitPrice)
    {
        var aggregate = new OrderAggregate();
        aggregate.Id = Guid.NewGuid();

        var @event = new OrderCreatedEvent(
            EventId: Guid.NewGuid(),
            AggregateId: aggregate.Id,
            OccurredAt: DateTimeOffset.UtcNow,
            EventType: nameof(OrderCreatedEvent),
            Version: 1,
            FundId: fundId,
            Symbol: symbol,
            Side: side,
            Quantity: quantity,
            LimitPrice: limitPrice
        );

        aggregate.ApplyEvent(@event);
        aggregate._uncommittedEvents.Add(@event);

        return aggregate;
    }

    public static OrderAggregate Rehydrate(Guid id, IEnumerable<IDomainEvent> eventStream)
    {
        var aggregate = new OrderAggregate { Id = id };

        foreach (var @event in eventStream)
        {
            aggregate.ApplyEvent(@event);
            aggregate._eventStream.Add(@event);
        }

        aggregate._uncommittedEvents.Clear();

        return aggregate;
    }

    public void StartRiskValidation()
    {
        _stateMachine.Fire(OrderTrigger.StartRiskValidation);

        var @event = new OrderRiskValidationStartedEvent(
            EventId: Guid.NewGuid(),
            AggregateId: Id,
            OccurredAt: DateTimeOffset.UtcNow,
            EventType: nameof(OrderRiskValidationStartedEvent),
            Version: Version + 1
        );

        ApplyEvent(@event);
        _uncommittedEvents.Add(@event);
    }

    public void Approve()
    {
        _stateMachine.Fire(OrderTrigger.Approve);

        var @event = new OrderApprovedEvent(
            EventId: Guid.NewGuid(),
            AggregateId: Id,
            OccurredAt: DateTimeOffset.UtcNow,
            EventType: nameof(OrderApprovedEvent),
            Version: Version + 1
        );

        ApplyEvent(@event);
        _uncommittedEvents.Add(@event);
    }

    public void Reject(string reason)
    {
        _stateMachine.Fire(OrderTrigger.Reject);

        var @event = new OrderRejectedEvent(
            EventId: Guid.NewGuid(),
            AggregateId: Id,
            OccurredAt: DateTimeOffset.UtcNow,
            EventType: nameof(OrderRejectedEvent),
            Version: Version + 1,
            Reason: reason
        );

        ApplyEvent(@event);
        _uncommittedEvents.Add(@event);
    }

    public void Submit()
    {
        _stateMachine.Fire(OrderTrigger.Submit);

        var @event = new OrderSubmittedEvent(
            EventId: Guid.NewGuid(),
            AggregateId: Id,
            OccurredAt: DateTimeOffset.UtcNow,
            EventType: nameof(OrderSubmittedEvent),
            Version: Version + 1
        );

        ApplyEvent(@event);
        _uncommittedEvents.Add(@event);
    }

    public IReadOnlyList<IDomainEvent> DequeueUncommittedEvents()
    {
        var events = _uncommittedEvents.ToList();
        _uncommittedEvents.Clear();
        return events;
    }

    private void ApplyEvent(IDomainEvent @event)
    {
        switch (@event)
        {
            case OrderCreatedEvent e:
                FundId = e.FundId;
                Symbol = e.Symbol;
                Side = e.Side;
                Quantity = e.Quantity;
                LimitPrice = e.LimitPrice;
                _currentState = OrderState.New;
                break;

            case OrderRiskValidationStartedEvent:
                _currentState = OrderState.RiskValidating;
                break;

            case OrderApprovedEvent:
                _currentState = OrderState.Approved;
                break;

            case OrderRejectedEvent:
                _currentState = OrderState.Rejected;
                break;

            case OrderSubmittedEvent:
                _currentState = OrderState.Submitted;
                break;
        }

        _eventStream.Add(@event);
    }

    private void ConfigureStateMachine()
    {
        _stateMachine.Configure(OrderState.New)
            .Permit(OrderTrigger.StartRiskValidation, OrderState.RiskValidating);

        _stateMachine.Configure(OrderState.RiskValidating)
            .Permit(OrderTrigger.Approve, OrderState.Approved)
            .Permit(OrderTrigger.Reject, OrderState.Rejected);

        _stateMachine.Configure(OrderState.Approved)
            .Permit(OrderTrigger.Submit, OrderState.Submitted);

        _stateMachine.Configure(OrderState.Rejected);

        _stateMachine.Configure(OrderState.Submitted);
    }
}
