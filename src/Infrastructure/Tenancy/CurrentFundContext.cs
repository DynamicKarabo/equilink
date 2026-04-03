using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace EquiLink.Infrastructure.Tenancy;

public class CurrentFundContext(IHttpContextAccessor httpContextAccessor) : ICurrentFundContext
{
    private const string FundIdClaimType = "fund_id";

    public Guid? FundId
    {
        get
        {
            var fundIdClaim = httpContextAccessor.HttpContext?.User?.FindFirst(FundIdClaimType);

            if (fundIdClaim == null || !Guid.TryParse(fundIdClaim.Value, out var fundId))
            {
                return null;
            }

            return fundId;
        }
    }

    public bool HasFundContext => FundId.HasValue;
}
