using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EquiLink.Infrastructure.DataTier;

public class ConnectionStringProvider(
    IConfiguration configuration,
    ILogger<ConnectionStringProvider> logger) : IConnectionStringProvider
{
    private readonly string _primary = configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("Postgres connection string is required.");

    private readonly string? _replica = configuration.GetConnectionString("PostgresReadOnly");

    public string GetWriteConnectionString() => _primary;

    public string GetReadConnectionString()
    {
        if (string.IsNullOrEmpty(_replica))
        {
            logger.LogWarning("No read replica configured, falling back to primary for reads");
            return _primary;
        }

        return _replica;
    }
}
