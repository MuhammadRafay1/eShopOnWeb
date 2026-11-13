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
/// Flow 4 — operator action: realign the portfolio of every investor who follows a model.
/// Restricted to the administrator role.
/// </summary>
public class CreateRealignmentEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/models/{modelId:int}/realignments",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int modelId, IManagedPortfolioService service, CancellationToken ct) =>
            {
                var result = await service.CreateRealignmentAsync(modelId, ct);
                return result.Status switch
                {
                    PortfolioOperationStatus.Ok => Results.Ok(new
                    {
                        realignmentId = result.Value!.RealignmentId,
                        status = result.Value.Status
                    }),
                    PortfolioOperationStatus.NotFound => Results.NotFound(new { message = result.Message }),
                    _ => Results.BadRequest(new { message = result.Message })
                };
            })
            .Produces<object>()
            .WithTags("PortfolioEndpoints");
    }
}
