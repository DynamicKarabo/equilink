namespace EquiLink.Infrastructure.Tenancy;

public interface ICurrentFundContext
{
    Guid? FundId { get; }
    bool HasFundContext { get; }
}
