namespace EquiLink.Domain.Aggregates.Fund;

public class Fund
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string ManagerName { get; private set; } = string.Empty;
    public FundStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public FundRiskLimits RiskLimits { get; private set; } = null!;

    private Fund() { }

    public static Fund Create(Guid id, string name, string managerName, FundRiskLimitTemplate template)
    {
        var fund = new Fund
        {
            Id = id,
            Name = name,
            ManagerName = managerName,
            Status = FundStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            RiskLimits = FundRiskLimits.FromTemplate(template)
        };

        return fund;
    }

    public void Activate()
    {
        if (Status != FundStatus.Pending)
        {
            throw new InvalidOperationException($"Fund can only be activated from Pending state. Current state: {Status}");
        }

        Status = FundStatus.Active;
    }

    public void Suspend()
    {
        if (Status == FundStatus.Closed)
        {
            throw new InvalidOperationException("Cannot suspend a closed fund.");
        }

        Status = FundStatus.Suspended;
    }

    public void UpdateRiskLimits(decimal maxOrderSize, decimal dailyLossLimit, decimal concentrationLimit)
    {
        RiskLimits.MaxOrderSize = maxOrderSize;
        RiskLimits.DailyLossLimit = dailyLossLimit;
        RiskLimits.ConcentrationLimit = concentrationLimit;
    }
}
