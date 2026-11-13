using System.Linq;
using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.PublicApi.Portfolio.Services;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.PortfolioEndpoints;

/// <summary>GET /api/portfolio — the caller's portfolio as Upvest currently records it.</summary>
public class GetPortfolioEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ClaimsPrincipal user, PortfolioQueryService service, CancellationToken ct) =>
                await EndpointSupport.RunAsync(user, async shopperId =>
                {
                    var view = await service.GetPortfolioAsync(shopperId, ct);
                    if (view is null)
                        return Results.NotFound();
                    return Results.Ok(new PortfolioResponse
                    {
                        ModelId = view.ModelId,
                        Holdings = view.Holdings.Select(h => new HoldingResponse
                        {
                            InstrumentId = h.InstrumentId,
                            Quantity = h.Quantity,
                            Value = h.Value,
                        }).ToList(),
                        TotalValue = view.TotalValue,
                        PaidIn = view.PaidIn,
                    });
                }))
            .Produces<PortfolioResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags(EndpointSupport.Tag);
    }
}
