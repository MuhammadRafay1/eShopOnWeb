using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;
using MinimalApi.Endpoint;
using static Microsoft.eShopWeb.PublicApi.PortfolioEndpoints.PortfolioEndpointSupport;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>GET /api/portfolio — the caller's portfolio as Upvest currently records it.</summary>
public class GetPortfolioEndpoint : IEndpoint<IResult, IUpvestPortfolioService>
{
    private readonly IHttpContextAccessor _http;
    public GetPortfolioEndpoint(IHttpContextAccessor http) => _http = http;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (IUpvestPortfolioService service) => await HandleAsync(service))
            .Produces<PortfolioResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(IUpvestPortfolioService service)
    {
        var ctx = _http.HttpContext!;
        var shopper = ResolveShopper(ctx.User);
        if (shopper is null) return Results.Unauthorized();

        try
        {
            var result = await service.GetPortfolioAsync(shopper, ctx.RequestAborted);
            return Results.Ok(new PortfolioResponse
            {
                ModelId = result.ModelId,
                Holdings = result.Holdings.Select(h => new PortfolioHoldingDto
                {
                    InstrumentId = h.InstrumentId,
                    Quantity = h.Quantity,
                    Value = decimal.Round(h.Value, 2, MidpointRounding.AwayFromZero)
                }).ToList(),
                TotalValue = decimal.Round(result.TotalValue, 2, MidpointRounding.AwayFromZero),
                PaidIn = decimal.Round(result.PaidIn, 2, MidpointRounding.AwayFromZero)
            });
        }
        catch (Exception ex)
        {
            return TryMapError(ex) ?? throw ex;
        }
    }
}
