using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>The caller's portfolio as Upvest currently records it: model, holdings, total value and paid in.</summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class GetPortfolioEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<PortfolioResponse>
{
    private readonly IPortfolioService _portfolioService;

    public GetPortfolioEndpoint(IPortfolioService portfolioService) => _portfolioService = portfolioService;

    [HttpGet("api/portfolio")]
    [SwaggerOperation(Summary = "Your portfolio", Tags = new[] { "PortfolioEndpoints" })]
    public override async Task<ActionResult<PortfolioResponse>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var userName = User.Identity!.Name!;
        var view = await _portfolioService.GetPortfolioAsync(userName, cancellationToken);

        return Ok(new PortfolioResponse
        {
            ModelId = view.ModelId,
            Holdings = view.Holdings.Select(h => new HoldingDto
            {
                InstrumentId = h.InstrumentId,
                Quantity = h.Quantity,
                Value = decimal.Round(h.Value, 2)
            }).ToList(),
            TotalValue = decimal.Round(view.TotalValue, 2),
            PaidIn = decimal.Round(view.PaidIn, 2)
        });
    }
}
