using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Swashbuckle.AspNetCore.Annotations;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 5 — the caller's portfolio as Upvest currently records it.</summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class GetPortfolioEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<PortfolioApiResponse>
{
    private readonly IPortfolioService _service;

    public GetPortfolioEndpoint(IPortfolioService service) => _service = service;

    [HttpGet("api/portfolio")]
    [SwaggerOperation(Summary = "Get the caller's portfolio", Tags = new[] { "Portfolio" })]
    public override async Task<ActionResult<PortfolioApiResponse>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var shopperId = PortfolioApi.ShopperId(User);
        if (shopperId is null) return Unauthorized();

        try
        {
            var result = await _service.GetPortfolioAsync(shopperId, cancellationToken);
            return result is null ? NotFound() : Ok(PortfolioApi.ToResponse(result));
        }
        catch (PortfolioException ex)
        {
            return PortfolioApi.ToErrorResult(ex);
        }
    }
}
