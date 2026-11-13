using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Swashbuckle.AspNetCore.Annotations;
using Roles = BlazorShared.Authorization.Constants.Roles;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 4 — operator realigns every portfolio following a model. Administrator only.</summary>
[Authorize(Roles = Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class CreateRealignmentEndpoint : EndpointBaseAsync
    .WithRequest<string>
    .WithActionResult<RealignmentApiResponse>
{
    private readonly IRealignmentService _service;

    public CreateRealignmentEndpoint(IRealignmentService service) => _service = service;

    [HttpPost("api/portfolio/models/{modelId}/realignments")]
    [SwaggerOperation(Summary = "Realign portfolios following a model (operator)", Tags = new[] { "Portfolio" })]
    public override async Task<ActionResult<RealignmentApiResponse>> HandleAsync(
        [FromRoute(Name = "modelId")] string modelId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service.TriggerAsync(modelId, cancellationToken);
            return Ok(PortfolioApi.ToResponse(result));
        }
        catch (PortfolioException ex)
        {
            return PortfolioApi.ToErrorResult(ex);
        }
    }
}

/// <summary>Flow 4 — the status of a realignment at Upvest.</summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class GetRealignmentEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult<RealignmentApiResponse>
{
    private readonly IRealignmentService _service;

    public GetRealignmentEndpoint(IRealignmentService service) => _service = service;

    [HttpGet("api/portfolio/realignments/{realignmentId}")]
    [SwaggerOperation(Summary = "Get a realignment's status", Tags = new[] { "Portfolio" })]
    public override async Task<ActionResult<RealignmentApiResponse>> HandleAsync(
        [FromRoute(Name = "realignmentId")] Guid realignmentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service.GetAsync(realignmentId, cancellationToken);
            return result is null ? NotFound() : Ok(PortfolioApi.ToResponse(result));
        }
        catch (PortfolioException ex)
        {
            return PortfolioApi.ToErrorResult(ex);
        }
    }
}
