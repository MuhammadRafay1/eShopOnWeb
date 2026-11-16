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
/// </summary>
public class GetPortfolioEndpoint : IEndpoint<IResult, ClaimsPrincipal, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IPortfolioService service) => await HandleAsync(user, service))
            .Produces<ApplicationCore.Portfolio.PortfolioView>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(ClaimsPrincipal user, IPortfolioService service)
    {
        var shopperId = PortfolioCaller.ShopperId(user);
        var view = await service.GetPortfolioAsync(shopperId);
        return Results.Ok(view);
    }
}
