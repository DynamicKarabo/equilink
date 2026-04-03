namespace EquiLink.Infrastructure.Persistence.EventStore;

public class OrderEvent
{
    public Guid Id { get; set; }
    public Guid AggregateId { get; set; }
    public Guid FundId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
