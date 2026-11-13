using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>GET /api/portfolio — the caller's portfolio as Upvest currently records it.</summary>
public class GetPortfolioEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (HttpContext http, IInvestorPortfolioService svc, CancellationToken ct) =>
            {
                var shopper = PortfolioUser.Name(http.User);
                var p = await svc.GetPortfolioAsync(shopper, ct);
                var holdings = p.Holdings
                    .Select(h => new HoldingResponse(h.InstrumentId, h.Quantity, Round(h.Value)))
                    .ToList();
                return Results.Ok(new PortfolioResponse(p.ModelId, holdings, Round(p.TotalValue), Round(p.PaidIn)));
            })
            .Produces<PortfolioResponse>()
            .WithTags("PortfolioEndpoints");
    }

    private static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
