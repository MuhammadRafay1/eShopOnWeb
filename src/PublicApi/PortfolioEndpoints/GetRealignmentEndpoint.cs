using System;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Reports where a realignment has got to, as reflected by Upvest.</summary>
public class GetRealignmentEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/realignments/{realignmentId}",
            async (Guid realignmentId, IPortfolioService service, CancellationToken ct) =>
                await PortfolioEndpointHelpers.RunAsync(async () =>
                {
                    var view = await service.GetRealignmentAsync(realignmentId, ct);
                    return Results.Ok(new RealignmentResponse(view.RealignmentId, view.Status));
                }))
            .RequireJwt()
            .Produces<RealignmentResponse>()
            .WithTags("PortfolioEndpoints");
    }
}
