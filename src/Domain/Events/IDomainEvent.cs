namespace EquiLink.Domain.Events;

public interface IDomainEvent
{
    Guid EventId { get; }
    Guid AggregateId { get; }
    DateTimeOffset OccurredAt { get; }
    string EventType { get; }
    int Version { get; }
}
