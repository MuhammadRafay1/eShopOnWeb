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

/// <summary>
/// Flow 4 — reports where a realignment has got to. Available to any signed-in shopper.
/// </summary>
public class GetRealignmentEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/realignments/{realignmentId:int}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int realignmentId, IManagedPortfolioService service, CancellationToken ct) =>
            {
                var result = await service.GetRealignmentAsync(realignmentId, ct);
                if (result is null)
                {
                    return Results.NotFound(new { message = $"No realignment with id {realignmentId} exists." });
                }

                return Results.Ok(new { realignmentId = result.RealignmentId, status = result.Status });
            })
            .Produces<object>()
            .WithTags("PortfolioEndpoints");
    }
}
