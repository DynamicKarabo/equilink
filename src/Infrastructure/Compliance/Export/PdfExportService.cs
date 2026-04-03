using System.Text;
using System.Security.Cryptography;
using EquiLink.Infrastructure.Compliance;

namespace EquiLink.Infrastructure.Compliance.Export;

public class PdfExportService : IPdfExportService
{
    public Task<byte[]> ExportAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();

        sb.AppendLine("%PDF-1.4");
        sb.AppendLine("1 0 obj");
        sb.AppendLine("<< /Type /Catalog /Pages 2 0 R >>");
        sb.AppendLine("endobj");

        var content = BuildTextContent(records);
        sb.AppendLine("2 0 obj");
        sb.AppendLine("<< /Type /Page /Parent 3 0 R /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>");
        sb.AppendLine("endobj");

        sb.AppendLine("3 0 obj");
        sb.AppendLine("<< /Type /Pages /Kids [2 0 R] /Count 1 >>");
        sb.AppendLine("endobj");

        sb.AppendLine("4 0 obj");
        sb.AppendLine("<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>");
        sb.AppendLine("endobj");

        var streamContent = $"BT /F1 10 Tf 50 750 Td ({EscapePdfText("EquiLink Compliance Audit Report")}) Tj 0 -20 Td ({EscapePdfText($"Generated: {DateTimeOffset.UtcNow:O}")}) Tj 0 -20 Td ({EscapePdfText($"Records: {records.Count}")}) Tj 0 -30 Td ({EscapePdfText("OrderId | FundId | EventType | Version | OccurredAt | Payload")}) Tj";

        foreach (var record in records)
        {
            var line = $"{record.OrderId} | {record.FundId} | {record.EventType} | {record.Version} | {record.OccurredAt:O} | {record.Payload}";
            streamContent += $" 0 -12 Td ({EscapePdfText(line)}) Tj";
        }

        streamContent += " ET";

        sb.AppendLine($"5 0 obj");
        sb.AppendLine($"<< /Length {streamContent.Length} >>");
        sb.AppendLine("stream");
        sb.Append(streamContent);
        sb.AppendLine();
        sb.AppendLine("endstream");
        sb.AppendLine("endobj");

        sb.AppendLine("xref");
        sb.AppendLine("0 6");
        sb.AppendLine("0000000000 65535 f ");
        sb.AppendLine("0000000009 00000 n ");
        sb.AppendLine("0000000058 00000 n ");
        sb.AppendLine("0000000158 00000 n ");
        sb.AppendLine("0000000215 00000 n ");
        sb.AppendLine("0000000282 00000 n ");
        sb.AppendLine("trailer");
        sb.AppendLine("<< /Size 6 /Root 1 0 R >>");
        sb.AppendLine("startxref");
        sb.AppendLine("0");
        sb.AppendLine("%%EOF");

        var pdfBytes = Encoding.UTF8.GetBytes(sb.ToString());
        return Task.FromResult(pdfBytes);
    }

    public string ComputeSignature(byte[] pdfBytes)
    {
        var hash = SHA256.HashData(pdfBytes);
        return Convert.ToBase64String(hash);
    }

    private static string BuildTextContent(IReadOnlyList<AuditRecord> records)
    {
        var sb = new StringBuilder();
        sb.AppendLine("EquiLink Compliance Audit Report");
        sb.AppendLine($"Generated: {DateTimeOffset.UtcNow:O}");
        sb.AppendLine($"Records: {records.Count}");
        sb.AppendLine();
        sb.AppendLine("OrderId,FundId,EventType,Version,OccurredAt,Payload");

        foreach (var record in records)
        {
            sb.AppendLine($"{record.OrderId},{record.FundId},{record.EventType},{record.Version},{record.OccurredAt:O},{record.Payload}");
        }

        return sb.ToString();
    }

    private static string EscapePdfText(string text)
    {
        return text.Replace("\\", "\\\\")
                   .Replace("(", "\\(")
                   .Replace(")", "\\)")
                   .Replace("\n", "\\n")
                   .Replace("\r", "\\r")
                   .Replace("\t", "\\t");
    }
}
