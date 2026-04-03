using EquiLink.Domain.Events;

namespace EquiLink.Domain.Aggregates.Order.Events;

public record OrderRiskValidationStartedEvent(
    Guid EventId,
    Guid AggregateId,
    DateTimeOffset OccurredAt,
    string EventType,
    int Version
) : DomainEvent(EventId, AggregateId, OccurredAt, EventType, Version);
