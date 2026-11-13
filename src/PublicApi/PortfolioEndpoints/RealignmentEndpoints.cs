using System;
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

/// <summary>POST /api/portfolio/models/{modelId}/realignments — realign every portfolio following the model (operator only).</summary>
public class TriggerRealignmentEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/models/{modelId}/realignments",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (string modelId, IInvestorPortfolioService svc, CancellationToken ct) =>
            {
                var result = await svc.TriggerRealignmentAsync(modelId, ct);
                return Results.Ok(new RealignmentResponse(result.RealignmentId, result.Status));
            })
            .Produces<RealignmentResponse>()
            .WithTags("PortfolioEndpoints");
    }
}

/// <summary>GET /api/portfolio/realignments/{realignmentId} — where that realignment has got to.</summary>
public class GetRealignmentEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/realignments/{realignmentId:guid}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (Guid realignmentId, IInvestorPortfolioService svc, CancellationToken ct) =>
            {
                var result = await svc.GetRealignmentAsync(realignmentId, ct);
                return result is null
                    ? Results.NotFound()
                    : Results.Ok(new RealignmentResponse(result.RealignmentId, result.Status));
            })
            .Produces<RealignmentResponse>()
            .WithTags("PortfolioEndpoints");
    }
}
