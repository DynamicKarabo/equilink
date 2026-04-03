using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Dapper;
using EquiLink.Infrastructure.DataTier;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EquiLink.Infrastructure.Compliance;

public class WormArchivalService(
    IConnectionStringProvider connectionStringProvider,
    string blobContainerConnectionString,
    ILogger<WormArchivalService> logger) : IWormArchivalService
{
    public async Task ArchiveMonthlyPartitionsAsync(DateTimeOffset month, CancellationToken cancellationToken = default)
    {
        var monthStart = new DateTimeOffset(month.Year, month.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var monthEnd = monthStart.AddMonths(1);

        var blobContainerClient = new BlobContainerClient(blobContainerConnectionString, "equilink-audit-archive");
        await blobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobName = $"monthly/{monthStart:yyyy-MM}/order_events_{monthStart:yyyy-MM}.csv";
        var blobClient = blobContainerClient.GetBlobClient(blobName);

        if (await blobClient.ExistsAsync(cancellationToken))
        {
            logger.LogWarning("Archive for {Month} already exists, skipping", monthStart.ToString("yyyy-MM"));
            return;
        }

        var connectionString = connectionStringProvider.GetReadConnectionString();
        await using var connection = new NpgsqlConnection(connectionString);

        const string sql = """
            SELECT
                id, aggregate_id, fund_id, event_type, payload, version, occurred_at, created_at
            FROM order_events
            WHERE occurred_at >= @MonthStart AND occurred_at < @MonthEnd
            ORDER BY occurred_at ASC, version ASC
            """;

        var events = await connection.QueryAsync(sql, new { MonthStart = monthStart, MonthEnd = monthEnd });
        var eventList = events.ToList();

        if (eventList.Count == 0)
        {
            logger.LogInformation("No events found for {Month}, skipping archival", monthStart.ToString("yyyy-MM"));
            return;
        }

        using var memoryStream = new MemoryStream();
        await using var writer = new StreamWriter(memoryStream, leaveOpen: true);

        writer.WriteLine("id,aggregate_id,fund_id,event_type,payload,version,occurred_at,created_at");

        foreach (var ev in eventList)
        {
            writer.WriteLine($"{ev.id},{ev.aggregate_id},{ev.fund_id},{ev.event_type},\"{ev.payload}\",{ev.version},{ev.occurred_at},{ev.created_at}");
        }

        await writer.FlushAsync(cancellationToken);
        memoryStream.Position = 0;

        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = "text/csv"
            }
        };

        await blobClient.UploadAsync(memoryStream, uploadOptions, cancellationToken);

        logger.LogInformation(
            "Archived {EventCount} events for {Month} to {BlobName}",
            eventList.Count, monthStart.ToString("yyyy-MM"), blobName);
    }
}
