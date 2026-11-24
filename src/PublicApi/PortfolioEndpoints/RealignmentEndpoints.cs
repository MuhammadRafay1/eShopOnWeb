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

/// <summary>POST /api/portfolio/models/{modelId}/realignments — operator realigns a model's followers.</summary>
public class TriggerRealignmentEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/models/{modelId}/realignments",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string modelId, IManagedPortfolioService service, CancellationToken ct) =>
            {
                var result = await service.TriggerRealignmentAsync(modelId, ct);
                return Results.Created($"api/portfolio/realignments/{result.RealignmentId}", result.ToResponse());
            })
            .Produces<RealignmentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("PortfolioEndpoints");
    }
}

/// <summary>GET /api/portfolio/realignments/{realignmentId} — status of a realignment.</summary>
public class GetRealignmentEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/realignments/{realignmentId:guid}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (Guid realignmentId, IManagedPortfolioService service, CancellationToken ct) =>
            {
                var result = await service.GetRealignmentAsync(realignmentId, ct);
                return Results.Ok(result.ToResponse());
            })
            .Produces<RealignmentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("PortfolioEndpoints");
    }
}
