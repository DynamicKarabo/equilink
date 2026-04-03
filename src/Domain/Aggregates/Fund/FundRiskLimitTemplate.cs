namespace EquiLink.Domain.Aggregates.Fund;

public class FundRiskLimitTemplate
{
    public Guid Id { get; private set; }
    public string TemplateName { get; private set; } = string.Empty;
    public decimal MaxOrderSize { get; private set; }
    public decimal DailyLossLimit { get; private set; }
    public decimal ConcentrationLimit { get; private set; }

    private FundRiskLimitTemplate() { }

    public static FundRiskLimitTemplate Create(string templateName, decimal maxOrderSize, decimal dailyLossLimit, decimal concentrationLimit)
    {
        return new FundRiskLimitTemplate
        {
            Id = Guid.NewGuid(),
            TemplateName = templateName,
            MaxOrderSize = maxOrderSize,
            DailyLossLimit = dailyLossLimit,
            ConcentrationLimit = concentrationLimit
        };
    }
}
