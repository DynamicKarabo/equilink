namespace EquiLink.Infrastructure.Compliance;

public interface IComplianceAuditService
{
    Task<IReadOnlyList<AuditRecord>> GetAuditRecordsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    Task<byte[]> ExportToCsvAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default);
    Task<byte[]> ExportToPdfAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default);
}
