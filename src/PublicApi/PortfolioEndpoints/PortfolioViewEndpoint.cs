using System.Security.Claims;
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
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IManagedPortfolioService service, CancellationToken ct) =>
            {
                var unauthorized = user.RequireUserName(out var userName);
                if (unauthorized != null) return unauthorized;

                var result = await service.GetPortfolioAsync(userName, ct);
                return result is null ? Results.NotFound() : Results.Ok(result.ToResponse());
            })
            .Produces<PortfolioResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("PortfolioEndpoints");
    }
}
