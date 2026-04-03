namespace EquiLink.Infrastructure.Compliance;

public interface IWormArchivalService
{
    Task ArchiveMonthlyPartitionsAsync(DateTimeOffset month, CancellationToken cancellationToken = default);
}
