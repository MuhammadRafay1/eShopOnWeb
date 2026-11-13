using System;
using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.PublicApi.Portfolio.Services;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.PortfolioEndpoints;

/// <summary>POST /api/portfolio/models/{modelId}/realignments — operator realigns followers of a model (admin only).</summary>
public class TriggerRealignmentEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/models/{modelId}/realignments",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (string modelId, ClaimsPrincipal user, RealignmentService service, CancellationToken ct) =>
                await EndpointSupport.RunAsync(user, async _ =>
                {
                    var result = await service.TriggerAsync(modelId, ct);
                    return Results.Ok(new RealignmentResponse { RealignmentId = result.RealignmentId.ToString(), Status = result.Status });
                }))
            .Produces<RealignmentResponse>()
            .WithTags(EndpointSupport.Tag);
    }
}

/// <summary>GET /api/portfolio/realignments/{realignmentId} — where a realignment has got to.</summary>
public class GetRealignmentEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/realignments/{realignmentId}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (string realignmentId, ClaimsPrincipal user, RealignmentService service, CancellationToken ct) =>
                await EndpointSupport.RunAsync(user, async _ =>
                {
                    if (!Guid.TryParse(realignmentId, out var id))
                        return Results.NotFound();
                    var result = await service.GetStatusAsync(id, ct);
                    if (result is null)
                        return Results.NotFound();
                    return Results.Ok(new RealignmentResponse { RealignmentId = result.RealignmentId.ToString(), Status = result.Status });
                }))
            .Produces<RealignmentResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags(EndpointSupport.Tag);
    }
}
