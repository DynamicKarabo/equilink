namespace EquiLink.Domain.Events;

public abstract record DomainEvent(
    Guid EventId,
    Guid AggregateId,
    DateTimeOffset OccurredAt,
    string EventType,
    int Version
) : IDomainEvent;
