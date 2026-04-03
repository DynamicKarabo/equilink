using EquiLink.Domain.Events;

namespace EquiLink.Domain.Aggregates.Order.Events;

public record OrderRejectedEvent(
    Guid EventId,
    Guid AggregateId,
    DateTimeOffset OccurredAt,
    string EventType,
    int Version,
    string Reason
) : DomainEvent(EventId, AggregateId, OccurredAt, EventType, Version);
