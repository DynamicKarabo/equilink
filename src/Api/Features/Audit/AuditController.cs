using EquiLink.Infrastructure.Compliance;
using Microsoft.AspNetCore.Mvc;

namespace EquiLink.Api.Features.Audit;

[ApiController]
[Route("audit")]
public class AuditController(IComplianceAuditService auditService) : ControllerBase
{
    [HttpGet("orders")]
    public async Task<IActionResult> GetAuditOrders(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] string format = "csv",
        CancellationToken cancellationToken = default)
    {
        var records = await auditService.GetAuditRecordsAsync(from, to, cancellationToken);

        if (records.Count == 0)
        {
            return NoContent();
        }

        byte[] fileBytes;
        string contentType;
        string fileName;

        if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
        {
            fileBytes = await auditService.ExportToPdfAsync(records, cancellationToken);
            contentType = "application/pdf";
            fileName = $"equilink-audit-{from:yyyyMMdd}-{to:yyyyMMdd}.pdf";
        }
        else
        {
            fileBytes = await auditService.ExportToCsvAsync(records, cancellationToken);
            contentType = "text/csv";
            fileName = $"equilink-audit-{from:yyyyMMdd}-{to:yyyyMMdd}.csv";
        }

        return File(fileBytes, contentType, fileName);
    }
}
