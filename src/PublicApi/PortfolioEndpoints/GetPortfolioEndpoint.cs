using System.Linq;
using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>The caller's portfolio as Upvest currently records it.</summary>
public class GetPortfolioEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio",
            async (ClaimsPrincipal user, IPortfolioService service, CancellationToken ct) =>
                await PortfolioEndpointHelpers.RunAsync(user, async buyerId =>
                {
                    var view = await service.GetPortfolioAsync(buyerId, ct);
                    var holdings = view.Holdings
                        .Select(h => new HoldingResponse(h.InstrumentId, h.Quantity, decimal.Round(h.Value, 2)))
                        .ToList();
                    return Results.Ok(new PortfolioResponse(
                        view.ModelId,
                        holdings,
                        decimal.Round(view.TotalValue, 2),
                        decimal.Round(view.PaidIn, 2)));
                }))
            .RequireJwt()
            .Produces<PortfolioResponse>()
            .WithTags("PortfolioEndpoints");
    }
}
