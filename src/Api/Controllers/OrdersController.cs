using EquiLink.Api.Features.Orders.Commands;
using EquiLink.Api.Features.Orders.Dtos;
using EquiLink.Api.Features.Orders.Queries;
using EquiLink.Infrastructure.ReadModels;
using EquiLink.Infrastructure.Tenancy;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EquiLink.Api.Controllers;

public class OrdersController(ISender sender, ICurrentFundContext fundContext) : BaseApiController
{
    [HttpPost]
    [EnableRateLimiting("order")]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderRequest request,
        [FromHeader(Name = "X-Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var command = new CreateOrderCommand(
            FundId: fundContext.FundId ?? Guid.Empty,
            Symbol: request.Symbol,
            Side: request.Side,
            Quantity: request.Quantity,
            LimitPrice: request.LimitPrice,
            AssetClass: request.AssetClass,
            IdempotencyKey: idempotencyKey
        );

        var result = await sender.Send(command, cancellationToken);

        return Ok(new { OrderId = result.OrderId, Status = "New", PaperTrading = true });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrder(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetOrderQuery(id);
        var order = await sender.Send(query, cancellationToken);

        if (order == null)
        {
            return NotFound();
        }

        return Ok(order);
    }
}
