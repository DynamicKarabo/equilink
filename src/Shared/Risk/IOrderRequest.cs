namespace EquiLink.Shared.Risk;

public interface IOrderRequest
{
    Guid FundId { get; }
    string Symbol { get; }
    string Side { get; }
    decimal Quantity { get; }
    decimal? LimitPrice { get; }
}
