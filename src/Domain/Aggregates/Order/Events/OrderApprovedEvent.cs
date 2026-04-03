using EquiLink.Domain.Events;

namespace EquiLink.Domain.Aggregates.Order.Events;

public record OrderApprovedEvent(
    Guid EventId,
    Guid AggregateId,
    DateTimeOffset OccurredAt,
    string EventType,
    int Version
) : DomainEvent(EventId, AggregateId, OccurredAt, EventType, Version);
