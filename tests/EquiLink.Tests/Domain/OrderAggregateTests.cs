using System;
using EquiLink.Domain.Aggregates.Order;
using EquiLink.Shared.AssetClasses;
using FluentAssertions;
using Xunit;

namespace EquiLink.Tests.Domain;

public class OrderAggregateTests
{
    private readonly Guid _fundId = Guid.NewGuid();
    private const string Symbol = "AAPL";
    private const string Side = "BUY";
    private const decimal Quantity = 100m;
    private const decimal LimitPrice = 150.50m;

    [Fact]
    public void Create_ShouldInitializeOrderWithCorrectState()
    {
        var order = OrderAggregate.Create(_fundId, Symbol, Side, Quantity, LimitPrice, AssetClass.Equity);

        order.Id.Should().NotBeEmpty();
        order.FundId.Should().Be(_fundId);
        order.Symbol.Should().Be(Symbol);
        order.Side.Should().Be(Side);
        order.Quantity.Should().Be(Quantity);
        order.LimitPrice.Should().Be(LimitPrice);
        order.AssetClass.Should().Be(AssetClass.Equity);
        order.CurrentState.Should().Be(OrderState.New);
        order.Version.Should().Be(1);
    }

    [Fact]
    public void Create_ShouldEmitOrderCreatedEvent()
    {
        var order = OrderAggregate.Create(_fundId, Symbol, Side, Quantity, LimitPrice, AssetClass.Equity);

        order.UncommittedEvents.Should().HaveCount(1);
        var evt = order.UncommittedEvents.Single();
        evt.GetType().Name.Should().Be("OrderCreatedEvent");
    }

    [Fact]
    public void StartRiskValidation_ShouldTransitionFromNewToRiskValidating()
    {
        var order = OrderAggregate.Create(_fundId, Symbol, Side, Quantity, LimitPrice, AssetClass.Equity);

        order.StartRiskValidation();

        order.CurrentState.Should().Be(OrderState.RiskValidating);
        order.Version.Should().Be(2);
    }

    [Fact]
    public void StartRiskValidation_ShouldEmitEvent()
    {
        var order = OrderAggregate.Create(_fundId, Symbol, Side, Quantity, LimitPrice, AssetClass.Equity);

        order.StartRiskValidation();

        order.UncommittedEvents.Should().HaveCount(2);
    }

    [Fact]
    public void Approve_ShouldTransitionFromRiskValidatingToApproved()
    {
        var order = OrderAggregate.Create(_fundId, Symbol, Side, Quantity, LimitPrice, AssetClass.Equity);
        order.StartRiskValidation();

        order.Approve();

        order.CurrentState.Should().Be(OrderState.Approved);
    }

    [Fact]
    public void Reject_ShouldTransitionFromRiskValidatingToRejected()
    {
        var order = OrderAggregate.Create(_fundId, Symbol, Side, Quantity, LimitPrice, AssetClass.Equity);
        order.StartRiskValidation();

        order.Reject("Risk limit exceeded");

        order.CurrentState.Should().Be(OrderState.Rejected);
    }

    [Fact]
    public void Submit_ShouldTransitionFromApprovedToSubmitted()
    {
        var order = OrderAggregate.Create(_fundId, Symbol, Side, Quantity, LimitPrice, AssetClass.Equity);
        order.StartRiskValidation();
        order.Approve();

        order.Submit();

        order.CurrentState.Should().Be(OrderState.Submitted);
    }

    [Fact]
    public void StartRiskValidation_FromApproved_ShouldThrow()
    {
        var order = OrderAggregate.Create(_fundId, Symbol, Side, Quantity, LimitPrice, AssetClass.Equity);
        order.StartRiskValidation();
        order.Approve();

        var act = () => order.StartRiskValidation();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Approve_FromNew_ShouldThrow()
    {
        var order = OrderAggregate.Create(_fundId, Symbol, Side, Quantity, LimitPrice, AssetClass.Equity);

        var act = () => order.Approve();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Correct_ShouldUpdateQuantityAndEmitEvent()
    {
        var order = OrderAggregate.Create(_fundId, Symbol, Side, Quantity, LimitPrice, AssetClass.Equity);

        order.Correct("quantity", "100", "200", "Customer request");

        order.Quantity.Should().Be(200);
        order.UncommittedEvents.Should().HaveCount(2);
    }

    [Fact]
    public void Correct_ShouldUpdateLimitPriceAndEmitEvent()
    {
        var order = OrderAggregate.Create(_fundId, Symbol, Side, Quantity, LimitPrice, AssetClass.Equity);

        order.Correct("limitprice", "150.50", "155.00", "Price adjustment");

        order.LimitPrice.Should().Be(155);
    }

    [Fact]
    public void DequeueUncommittedEvents_ShouldClearUncommittedList()
    {
        var order = OrderAggregate.Create(_fundId, Symbol, Side, Quantity, LimitPrice, AssetClass.Equity);
        order.StartRiskValidation();
        order.Approve();

        var events = order.DequeueUncommittedEvents();

        events.Should().HaveCount(3);
        order.UncommittedEvents.Should().BeEmpty();
    }

    [Fact]
    public void Rehydrate_ShouldRestoreAggregateState()
    {
        var order = OrderAggregate.Create(_fundId, Symbol, Side, Quantity, LimitPrice, AssetClass.Equity);
        order.StartRiskValidation();
        order.Approve();
        var events = order.DequeueUncommittedEvents();

        var rehydrated = OrderAggregate.Rehydrate(order.Id, events);

        rehydrated.Id.Should().Be(order.Id);
        rehydrated.FundId.Should().Be(_fundId);
        rehydrated.Symbol.Should().Be(Symbol);
        rehydrated.Quantity.Should().Be(Quantity);
        rehydrated.CurrentState.Should().Be(OrderState.Approved);
    }

    [Fact]
    public void Rehydrate_WithCorrectedEvent_ShouldApplyCorrection()
    {
        var order = OrderAggregate.Create(_fundId, Symbol, Side, Quantity, LimitPrice, AssetClass.Equity);
        order.Correct("quantity", "100", "500", "Error correction");
        var events = order.DequeueUncommittedEvents();

        var rehydrated = OrderAggregate.Rehydrate(order.Id, events);

        rehydrated.Quantity.Should().Be(500);
    }

    [Theory]
    [InlineData("New", OrderTrigger.StartRiskValidation, OrderState.RiskValidating)]
    [InlineData("RiskValidating", OrderTrigger.Approve, OrderState.Approved)]
    [InlineData("RiskValidating", OrderTrigger.Reject, OrderState.Rejected)]
    [InlineData("Approved", OrderTrigger.Submit, OrderState.Submitted)]
    public void StateMachine_ValidTransitions(string state, OrderTrigger trigger, OrderState expectedState)
    {
        var order = OrderAggregate.Create(_fundId, Symbol, Side, Quantity, LimitPrice, AssetClass.Equity);

        switch (state)
        {
            case "New":
                break;
            case "RiskValidating":
                order.StartRiskValidation();
                break;
            case "Approved":
                order.StartRiskValidation();
                order.Approve();
                break;
        }

        switch (trigger)
        {
            case OrderTrigger.StartRiskValidation:
                order.StartRiskValidation();
                break;
            case OrderTrigger.Approve:
                order.Approve();
                break;
            case OrderTrigger.Reject:
                order.Reject("test");
                break;
            case OrderTrigger.Submit:
                order.Submit();
                break;
        }

        order.CurrentState.Should().Be(expectedState);
    }
}
