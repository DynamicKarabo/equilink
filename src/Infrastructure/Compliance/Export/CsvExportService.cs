using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CsvHelper;
using EquiLink.Infrastructure.Compliance;

namespace EquiLink.Infrastructure.Compliance.Export;

public class CsvExportService : ICsvExportService
{
    public async Task<byte[]> ExportAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default)
    {
        await using var memoryStream = new MemoryStream();
        await using var writer = new StreamWriter(memoryStream, Encoding.UTF8, leaveOpen: true);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        csv.WriteRecords(records);
        await writer.FlushAsync(cancellationToken);

        return memoryStream.ToArray();
    }

    public string ComputeSignature(byte[] csvBytes)
    {
        var hash = SHA256.HashData(csvBytes);
        return Convert.ToBase64String(hash);
    }
}
