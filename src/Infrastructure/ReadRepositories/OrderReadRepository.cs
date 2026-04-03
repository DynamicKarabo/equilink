using Dapper;
using EquiLink.Infrastructure.DataTier;
using EquiLink.Infrastructure.ReadModels;
using Npgsql;

namespace EquiLink.Infrastructure.ReadRepositories;

public class OrderReadRepository(IConnectionStringProvider connectionStringProvider) : IOrderReadRepository
{
    public async Task<OrderSummaryProjection?> GetByIdAsync(
        Guid orderId, Guid fundId, CancellationToken cancellationToken = default)
    {
        var connectionString = connectionStringProvider.GetReadConnectionString();

        await using var connection = new NpgsqlConnection(connectionString);

        const string sql = """
            SELECT
                e.aggregate_id AS OrderId,
                e.fund_id AS FundId,
                e.payload->>'symbol' AS Symbol,
                e.payload->>'side' AS Side,
                (e.payload->>'quantity')::DECIMAL AS Quantity,
                CASE
                    WHEN e.payload ? 'limitPrice' AND e.payload->>'limitPrice' IS NOT NULL
                    THEN (e.payload->>'limitPrice')::DECIMAL
                    ELSE NULL
                END AS LimitPrice,
                e.event_type AS Status,
                e.occurred_at AS CreatedAt,
                e.created_at AS UpdatedAt
            FROM order_events e
            WHERE e.aggregate_id = @OrderId
              AND e.fund_id = @FundId
            ORDER BY e.version DESC
            LIMIT 1
            """;

        return await connection.QueryFirstOrDefaultAsync<OrderSummaryProjection>(
            sql, new { OrderId = orderId, FundId = fundId });
    }
}
