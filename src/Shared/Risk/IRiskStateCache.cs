namespace EquiLink.Shared.Risk;

public interface IRiskStateCache
{
    Task<HashSet<string>> GetBlacklistedSymbolsAsync(string fundId, CancellationToken cancellationToken = default);
    Task<decimal?> GetMaxOrderSizeAsync(string fundId, CancellationToken cancellationToken = default);
    Task<decimal?> GetCurrentExposureAsync(string fundId, CancellationToken cancellationToken = default);
}
