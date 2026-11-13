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
/// POST /api/portfolio/models/{modelId}/realignments — operator action: realign every portfolio that
/// follows the model back to its current weights.
/// </summary>
public class TriggerRealignmentEndpoint : IEndpoint<IResult, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/models/{modelId}/realignments",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string modelId, IPortfolioService service, CancellationToken ct) =>
            {
                var realignment = await service.TriggerRealignmentAsync(modelId, ct);
                return Results.Ok(new RealignmentResponse
                {
                    RealignmentId = realignment.RealignmentId,
                    Status = StatusText.For(realignment.Status),
                });
            })
            .Produces<RealignmentResponse>()
            .WithTags("PortfolioEndpoints");
    }

    // Logic is in the route lambda (it needs the route value and cancellation token).
    public Task<IResult> HandleAsync(IPortfolioService service) => Task.FromResult(Results.BadRequest() as IResult);
}
