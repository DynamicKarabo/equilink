namespace EquiLink.Infrastructure.Compliance.Export;

public interface ICsvExportService
{
    Task<byte[]> ExportAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default);
    string ComputeSignature(byte[] csvBytes);
}
