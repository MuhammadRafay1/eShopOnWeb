using System.Threading;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Operator action: realigns the portfolio of every investor who follows a model.</summary>
public class TriggerRealignmentEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/models/{modelId}/realignments",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (string modelId, IPortfolioService service, CancellationToken ct) =>
                await PortfolioEndpointHelpers.RunAsync(async () =>
                {
                    var view = await service.TriggerRealignmentAsync(modelId, ct);
                    return Results.Ok(new RealignmentResponse(view.RealignmentId, view.Status));
                }))
            .Produces<RealignmentResponse>()
            .WithTags("PortfolioEndpoints");
    }
}
