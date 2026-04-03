using System.ComponentModel.DataAnnotations;

namespace EquiLink.Api.Features.Orders.Dtos;

public record CreateOrderRequest(
    [Required] string Symbol,
    [Required] string Side,
    [Required, Range(0.00000001, double.MaxValue)] decimal Quantity,
    decimal? LimitPrice = null
);
