namespace EquiLink.Infrastructure.Compliance;

public record AuditRecord(
    Guid OrderId,
    Guid FundId,
    string EventType,
    string Payload,
    int Version,
    DateTimeOffset OccurredAt
);
