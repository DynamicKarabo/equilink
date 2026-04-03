namespace EquiLink.Infrastructure.Compliance.Export;

public interface IPdfExportService
{
    Task<byte[]> ExportAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default);
    string ComputeSignature(byte[] pdfBytes);
}
