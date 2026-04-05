using System.Text.Json;
using EquiLink.Domain.Aggregates.Order.Events;
using EquiLink.Domain.EventStore;
using EquiLink.Domain.Events;
using EquiLink.Infrastructure.Persistence;
using EquiLink.Infrastructure.Persistence.EventStore;
using EquiLink.Infrastructure.Snapshots;
using EquiLink.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EquiLink.Infrastructure.Persistence.EventStore;

public class SnapshottingEventStore : IEventStore
{
    private readonly EquiLinkDbContext _dbContext;
    private readonly ISnapshotStore _snapshotStore;
    private readonly ICurrentFundContext _currentFundContext;
    private const int SnapshotInterval = 10;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerOptions.Default)
    {
        PropertyNameCaseInsensitive = true
    };

    public SnapshottingEventStore(
        EquiLinkDbContext dbContext,
        ISnapshotStore snapshotStore,
        ICurrentFundContext currentFundContext)
    {
        _dbContext = dbContext;
        _snapshotStore = snapshotStore;
        _currentFundContext = currentFundContext;
    }

    public async Task AppendAsync(
        Guid aggregateId,
        IReadOnlyList<IDomainEvent> events,
        CancellationToken cancellationToken = default)
    {
        var fundId = _currentFundContext.FundId
            ?? events.OfType<OrderCreatedEvent>().FirstOrDefault()?.FundId
            ?? Guid.Empty;

        foreach (var domainEvent in events)
        {
            var entity = new OrderEvent
            {
                Id = domainEvent.EventId,
                AggregateId = domainEvent.AggregateId,
                FundId = fundId,
                EventType = domainEvent.EventType,
                Payload = SerializePayload(domainEvent),
                Version = domainEvent.Version,
                OccurredAt = domainEvent.OccurredAt,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.OrderEvents.Add(entity);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var latestVersion = events.Max(e => e.Version);
        if (latestVersion % SnapshotInterval == 0)
        {
            var orderSnapshot = new OrderSnapshot
            {
                FundId = fundId,
                Symbol = events.OfType<OrderCreatedEvent>().FirstOrDefault()?.Symbol ?? "",
                Side = events.OfType<OrderCreatedEvent>().FirstOrDefault()?.Side ?? "",
                Quantity = events.OfType<OrderCreatedEvent>().FirstOrDefault()?.Quantity ?? 0,
                LimitPrice = events.OfType<OrderCreatedEvent>().FirstOrDefault()?.LimitPrice,
                AssetClass = events.OfType<OrderCreatedEvent>().FirstOrDefault()?.AssetClass ?? "Equity",
                CurrentState = GetCurrentState(events)
            };

            await _snapshotStore.SaveSnapshotAsync(aggregateId, latestVersion, orderSnapshot, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<IDomainEvent>> LoadAsync(
        Guid aggregateId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _snapshotStore.LoadSnapshotAsync<OrderSnapshot>(aggregateId, cancellationToken);
        
        int startVersion = 1;
        
        if (snapshot != null)
        {
            startVersion = snapshot.CurrentState switch
            {
                "New" => 1,
                "RiskValidating" => 2,
                "Approved" => 3,
                "Rejected" => 3,
                "Submitted" => 4,
                _ => 1
            };
        }

        var entities = await _dbContext.IgnoreTenantFilter<OrderEvent>()
            .AsNoTracking()
            .Where(e => e.AggregateId == aggregateId && e.Version >= startVersion)
            .OrderBy(e => e.Version)
            .ToListAsync(cancellationToken);

        return entities.Select(DeserializeEvent).ToList();
    }

    private static string GetCurrentState(IReadOnlyList<IDomainEvent> events)
    {
        return events.LastOrDefault() switch
        {
            OrderCreatedEvent => "New",
            OrderRiskValidationStartedEvent => "RiskValidating",
            OrderApprovedEvent => "Approved",
            OrderRejectedEvent => "Rejected",
            OrderSubmittedEvent => "Submitted",
            _ => "Unknown"
        };
    }

    private static string SerializePayload(IDomainEvent @event)
    {
        return @event switch
        {
            OrderCreatedEvent e => JsonSerializer.Serialize(new
            {
                e.FundId, e.Symbol, e.Side, e.Quantity, e.LimitPrice, e.AssetClass
            }, JsonOptions),
            OrderRejectedEvent e => JsonSerializer.Serialize(new
            {
                e.Reason
            }, JsonOptions),
            OrderCorrectedEvent e => JsonSerializer.Serialize(new
            {
                e.OriginalField, e.OriginalValue, e.CorrectedValue, e.Reason
            }, JsonOptions),
            _ => JsonSerializer.Serialize(new { }, JsonOptions)
        };
    }

    private static IDomainEvent DeserializeEvent(OrderEvent entity)
    {
        var payload = JsonDocument.Parse(entity.Payload);

        return entity.EventType switch
        {
            nameof(OrderCreatedEvent) => new OrderCreatedEvent(
                EventId: entity.Id,
                AggregateId: entity.AggregateId,
                OccurredAt: entity.OccurredAt,
                EventType: entity.EventType,
                Version: entity.Version,
                FundId: payload.RootElement.GetProperty("fundId").GetGuid(),
                Symbol: payload.RootElement.GetProperty("symbol").GetString()!,
                Side: payload.RootElement.GetProperty("side").GetString()!,
                Quantity: payload.RootElement.GetProperty("quantity").GetDecimal(),
                LimitPrice: payload.RootElement.TryGetProperty("limitPrice", out var lp) && lp.ValueKind != JsonValueKind.Null
                    ? lp.GetDecimal()
                    : null,
                AssetClass: payload.RootElement.GetProperty("assetClass").GetString()!),

            nameof(OrderRiskValidationStartedEvent) => new OrderRiskValidationStartedEvent(
                EventId: entity.Id,
                AggregateId: entity.AggregateId,
                OccurredAt: entity.OccurredAt,
                EventType: entity.EventType,
                Version: entity.Version),

            nameof(OrderApprovedEvent) => new OrderApprovedEvent(
                EventId: entity.Id,
                AggregateId: entity.AggregateId,
                OccurredAt: entity.OccurredAt,
                EventType: entity.EventType,
                Version: entity.Version),

            nameof(OrderRejectedEvent) => new OrderRejectedEvent(
                EventId: entity.Id,
                AggregateId: entity.AggregateId,
                OccurredAt: entity.OccurredAt,
                EventType: entity.EventType,
                Version: entity.Version,
                Reason: payload.RootElement.GetProperty("reason").GetString()!),

            nameof(OrderSubmittedEvent) => new OrderSubmittedEvent(
                EventId: entity.Id,
                AggregateId: entity.AggregateId,
                OccurredAt: entity.OccurredAt,
                EventType: entity.EventType,
                Version: entity.Version),

            nameof(OrderCorrectedEvent) => new OrderCorrectedEvent(
                EventId: entity.Id,
                AggregateId: entity.AggregateId,
                OccurredAt: entity.OccurredAt,
                EventType: entity.EventType,
                Version: entity.Version,
                OriginalField: payload.RootElement.GetProperty("originalField").GetString()!,
                OriginalValue: payload.RootElement.GetProperty("originalValue").GetString()!,
                CorrectedValue: payload.RootElement.GetProperty("correctedValue").GetString()!,
                Reason: payload.RootElement.GetProperty("reason").GetString()!),

            _ => throw new InvalidOperationException($"Unknown event type: {entity.EventType}")
        };
    }
}

public class OrderSnapshot
{
    public Guid FundId { get; set; }
    public string Symbol { get; set; } = "";
    public string Side { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal? LimitPrice { get; set; }
    public string AssetClass { get; set; } = "Equity";
    public string CurrentState { get; set; } = "New";
}
