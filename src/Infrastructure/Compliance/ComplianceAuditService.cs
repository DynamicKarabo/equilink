using Dapper;
using EquiLink.Infrastructure.Compliance.Export;
using EquiLink.Infrastructure.DataTier;
using Npgsql;

namespace EquiLink.Infrastructure.Compliance;

public class ComplianceAuditService(
    IConnectionStringProvider connectionStringProvider,
    ICsvExportService csvExport,
    IPdfExportService pdfExport) : IComplianceAuditService
{
    public async Task<IReadOnlyList<AuditRecord>> GetAuditRecordsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var connectionString = connectionStringProvider.GetReadConnectionString();

        await using var connection = new NpgsqlConnection(connectionString);

        const string sql = """
            SELECT
                aggregate_id AS OrderId,
                fund_id AS FundId,
                event_type AS EventType,
                payload AS Payload,
                version AS Version,
                occurred_at AS OccurredAt
            FROM order_events
            WHERE occurred_at >= @From AND occurred_at <= @To
            ORDER BY occurred_at ASC, version ASC
            """;

        var records = await connection.QueryAsync<AuditRecord>(sql, new { From = from, To = to });
        return records.ToList();
    }

    public async Task<byte[]> ExportToCsvAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default)
    {
        return await csvExport.ExportAsync(records, cancellationToken);
    }

    public async Task<byte[]> ExportToPdfAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default)
    {
        return await pdfExport.ExportAsync(records, cancellationToken);
    }
}
