using System.Text.Json;
using EquiLink.Domain.Aggregates.Order.Events;
using EquiLink.Domain.EventStore;
using EquiLink.Domain.Events;
using EquiLink.Infrastructure.Persistence;
using EquiLink.Infrastructure.Persistence.EventStore;
using EquiLink.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EquiLink.Infrastructure.Persistence.EventStore;

public class EventStore(EquiLinkDbContext dbContext, ICurrentFundContext currentFundContext) : IEventStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerOptions.Default)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task AppendAsync(
        Guid aggregateId,
        IReadOnlyList<IDomainEvent> events,
        CancellationToken cancellationToken = default)
    {
        var fundId = currentFundContext.FundId
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

            dbContext.OrderEvents.Add(entity);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IDomainEvent>> LoadAsync(
        Guid aggregateId,
        CancellationToken cancellationToken = default)
    {
        var entities = await dbContext.IgnoreTenantFilter<OrderEvent>()
            .AsNoTracking()
            .Where(e => e.AggregateId == aggregateId)
            .OrderBy(e => e.Version)
            .ToListAsync(cancellationToken);

        return entities.Select(DeserializeEvent).ToList();
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
