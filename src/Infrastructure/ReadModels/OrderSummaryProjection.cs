namespace EquiLink.Infrastructure.ReadModels;

public record OrderSummaryProjection(
    Guid OrderId,
    Guid FundId,
    string Symbol,
    string Side,
    decimal Quantity,
    decimal? LimitPrice,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);
