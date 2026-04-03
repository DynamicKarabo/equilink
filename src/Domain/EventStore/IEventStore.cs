using EquiLink.Domain.Events;

namespace EquiLink.Domain.EventStore;

public interface IEventStore
{
    Task AppendAsync(Guid aggregateId, IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDomainEvent>> LoadAsync(Guid aggregateId, CancellationToken cancellationToken = default);
}
