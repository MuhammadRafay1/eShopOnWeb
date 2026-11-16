using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Operator action: realign every portfolio that follows a model back to its current weights.</summary>
[Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class TriggerRealignmentEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<RealignmentResponse>
{
    private readonly IPortfolioService _portfolioService;

    public TriggerRealignmentEndpoint(IPortfolioService portfolioService) => _portfolioService = portfolioService;

    [HttpPost("api/portfolio/models/{modelId}/realignments")]
    [SwaggerOperation(Summary = "Realign portfolios that follow a model (operator)", Tags = new[] { "PortfolioEndpoints" })]
    public override async Task<ActionResult<RealignmentResponse>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var modelId = (string)RouteData.Values["modelId"]!;
        var realignment = await _portfolioService.TriggerRealignmentAsync(modelId, cancellationToken);
        return Ok(new RealignmentResponse
        {
            RealignmentId = realignment.RealignmentId,
            Status = PortfolioStatusText.From(realignment.Status)
        });
    }
}

/// <summary>Reports where a realignment has got to at Upvest.</summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class GetRealignmentEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<RealignmentResponse>
{
    private readonly IPortfolioService _portfolioService;

    public GetRealignmentEndpoint(IPortfolioService portfolioService) => _portfolioService = portfolioService;

    [HttpGet("api/portfolio/realignments/{realignmentId}")]
    [SwaggerOperation(Summary = "Realignment status", Tags = new[] { "PortfolioEndpoints" })]
    public override async Task<ActionResult<RealignmentResponse>> HandleAsync(CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse((string?)RouteData.Values["realignmentId"], out var realignmentId))
        {
            return BadRequest("realignmentId must be a UUID.");
        }

        var status = await _portfolioService.GetRealignmentStatusAsync(realignmentId, cancellationToken);
        return Ok(new RealignmentResponse
        {
            RealignmentId = realignmentId,
            Status = PortfolioStatusText.From(status)
        });
    }
}
