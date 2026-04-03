using EquiLink.Api.Features.Funds.Commands;
using EquiLink.Api.Features.Funds.Dtos;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EquiLink.Api.Controllers;

public class FundsController(ISender sender) : BaseApiController
{
    [HttpPost]
    public async Task<IActionResult> CreateFund(
        [FromBody] CreateFundRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateFundCommand(
            request.Name,
            request.ManagerName,
            request.RiskLimitTemplateName,
            request.MaxOrderSize,
            request.DailyLossLimit,
            request.ConcentrationLimit
        );

        var result = await sender.Send(command, cancellationToken);

        return CreatedAtAction(nameof(GetFund), new { id = result.FundId }, new
        {
            FundId = result.FundId,
            Name = result.Name,
            Status = result.Status.ToString()
        });
    }

    [HttpGet("{id}")]
    public IActionResult GetFund(Guid id)
    {
        return Ok(new { FundId = id, Status = "NotImplemented" });
    }
}
