using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Flow 5 — the caller's portfolio as Upvest currently records it.
/// GET /api/portfolio
/// </summary>
public class GetPortfolioEndpoint : IEndpoint<IResult, string, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IPortfolioService service) => await HandleAsync(user.BuyerId(), service))
            .Produces<PortfolioResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(string buyerId, IPortfolioService service)
    {
        var view = await service.GetPortfolioAsync(buyerId, default);
        var response = new PortfolioResponse(
            view.ModelId,
            view.Holdings
                .Select(h => new HoldingResponse(h.InstrumentId, h.Quantity, PortfolioEndpointHelpers.Money(h.Value)))
                .ToList(),
            PortfolioEndpointHelpers.Money(view.TotalValue),
            PortfolioEndpointHelpers.Money(view.PaidIn));
        return Results.Ok(response);
    }
}
