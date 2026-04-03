namespace EquiLink.Domain.Aggregates.Fund;

public class FundRiskLimits
{
    public Guid FundId { get; private set; }
    public decimal MaxOrderSize { get; set; }
    public decimal DailyLossLimit { get; set; }
    public decimal ConcentrationLimit { get; set; }

    private FundRiskLimits() { }

    public static FundRiskLimits FromTemplate(FundRiskLimitTemplate template)
    {
        return new FundRiskLimits
        {
            MaxOrderSize = template.MaxOrderSize,
            DailyLossLimit = template.DailyLossLimit,
            ConcentrationLimit = template.ConcentrationLimit
        };
    }

    public static FundRiskLimits Create(Guid fundId, decimal maxOrderSize, decimal dailyLossLimit, decimal concentrationLimit)
    {
        return new FundRiskLimits
        {
            FundId = fundId,
            MaxOrderSize = maxOrderSize,
            DailyLossLimit = dailyLossLimit,
            ConcentrationLimit = concentrationLimit
        };
    }
}
