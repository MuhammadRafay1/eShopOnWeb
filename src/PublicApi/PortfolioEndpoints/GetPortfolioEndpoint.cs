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
/// Flow 5 — the caller's portfolio as Upvest currently records it: the model it follows, each fund
/// held with its quantity and current value, the total current value, and the total amount paid in.
/// </summary>
public class GetPortfolioEndpoint : IEndpoint<IResult, string, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IPortfolioService service) =>
            {
                var shopperId = user.GetShopperId();
                if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();
                return await HandleAsync(shopperId, service);
            })
            .Produces<PortfolioResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(string shopperId, IPortfolioService service)
    {
        var view = await service.GetPortfolioAsync(shopperId);
        return view is null ? Results.NotFound() : Results.Ok(view.ToResponse());
    }
}
