using EquiLink.Infrastructure.ReadModels;

namespace EquiLink.Infrastructure.ReadRepositories;

public interface IOrderReadRepository
{
    Task<OrderSummaryProjection?> GetByIdAsync(Guid orderId, Guid fundId, CancellationToken cancellationToken = default);
}
