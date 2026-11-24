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
/// Flow 4 — where a realignment has got to. Available to any signed-in shopper.
/// </summary>
public class GetRealignmentEndpoint : IEndpoint<IResult, string, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/realignments/{realignmentId}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string realignmentId, IPortfolioService service) =>
            {
                return await HandleAsync(realignmentId, service);
            })
            .Produces<RealignmentResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(string realignmentId, IPortfolioService service)
    {
        var view = await service.GetRealignmentAsync(realignmentId);
        return view is null ? Results.NotFound() : Results.Ok(view.ToResponse());
    }
}
