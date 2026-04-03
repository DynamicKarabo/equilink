using EquiLink.Domain.Events;

namespace EquiLink.Domain.Aggregates.Order.Events;

public record OrderCorrectedEvent(
    Guid EventId,
    Guid AggregateId,
    DateTimeOffset OccurredAt,
    string EventType,
    int Version,
    string OriginalField,
    string OriginalValue,
    string CorrectedValue,
    string Reason
) : DomainEvent(EventId, AggregateId, OccurredAt, EventType, Version);
