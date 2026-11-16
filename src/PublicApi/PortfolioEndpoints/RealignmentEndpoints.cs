using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 4 — POST /api/portfolio/models/{modelId}/realignments: operator realigns a model's followers.</summary>
public class TriggerRealignmentEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/models/{modelId}/realignments",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
                       AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                string modelId,
                IRealignmentService service,
                CancellationToken ct) =>
            {
                try
                {
                    var realignment = await service.TriggerAsync(modelId, ct);
                    return Results.Ok(ToResponse(realignment));
                }
                catch (ModelNotFoundException ex)
                {
                    return Results.NotFound(new { message = ex.Message });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.Conflict(new { message = ex.Message });
                }
                catch (UpvestException ex)
                {
                    return PortfolioEndpointHelpers.ToResult(ex);
                }
            })
            .Produces<RealignmentResponse>()
            .WithTags("PortfolioEndpoints");
    }

    internal static RealignmentResponse ToResponse(Realignment r) => new()
    {
        RealignmentId = r.Id,
        Status = PortfolioStatusText.Of(r.Status)
    };
}

/// <summary>Flow 4 — GET /api/portfolio/realignments/{realignmentId}: where a realignment has got to.</summary>
public class GetRealignmentEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/realignments/{realignmentId:int}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                int realignmentId,
                IRealignmentService service,
                CancellationToken ct) =>
            {
                var realignment = await service.GetAsync(realignmentId, ct);
                if (realignment is null)
                    return Results.NotFound(new { message = $"Realignment {realignmentId} does not exist." });
                return Results.Ok(TriggerRealignmentEndpoint.ToResponse(realignment));
            })
            .Produces<RealignmentResponse>()
            .WithTags("PortfolioEndpoints");
    }
}
