namespace EquiLink.Infrastructure.ReadModels;

public class OrderSummaryProjection
{
    public Guid OrderId { get; set; }
    public Guid FundId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal? LimitPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
