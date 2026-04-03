using EquiLink.Domain.Events;

namespace EquiLink.Domain.Aggregates.Order.Events;

public record OrderCreatedEvent(
    Guid EventId,
    Guid AggregateId,
    DateTimeOffset OccurredAt,
    string EventType,
    int Version,
    Guid FundId,
    string Symbol,
    string Side,
    decimal Quantity,
    decimal? LimitPrice
) : DomainEvent(EventId, AggregateId, OccurredAt, EventType, Version);
