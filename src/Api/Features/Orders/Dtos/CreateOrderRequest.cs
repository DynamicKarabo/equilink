using System.ComponentModel.DataAnnotations;
using EquiLink.Shared.AssetClasses;

namespace EquiLink.Api.Features.Orders.Dtos;

public record CreateOrderRequest(
    [Required] string Symbol,
    [Required, RegularExpression("^(BUY|SELL)$", ErrorMessage = "Side must be BUY or SELL")] string Side,
    [Required, Range(0.00000001, double.MaxValue)] decimal Quantity,
    decimal? LimitPrice = null,
    AssetClass AssetClass = AssetClass.Equity
);
