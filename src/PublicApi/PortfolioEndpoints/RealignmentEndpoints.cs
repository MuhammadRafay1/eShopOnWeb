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
using static Microsoft.eShopWeb.PublicApi.PortfolioEndpoints.PortfolioEndpointHelpers;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 4 — operator action: realign every investor following a model to its current weights.</summary>
public class TriggerRealignmentEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/models/{modelId}/realignments",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (string modelId, IPortfolioService service, CancellationToken ct) =>
            {
                var result = await service.TriggerRealignmentAsync(modelId, ct);
                return ToHttpResult(result, v => new RealignmentResponse(v.RealignmentId, v.Status), StatusCodes.Status201Created);
            })
            .Produces<RealignmentResponse>(StatusCodes.Status201Created)
            .WithTags("PortfolioEndpoints");
    }
}

/// <summary>Flow 4 — report where a realignment has got to.</summary>
public class GetRealignmentEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/realignments/{realignmentId:guid}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (Guid realignmentId, IPortfolioService service, CancellationToken ct) =>
            {
                var result = await service.GetRealignmentAsync(realignmentId, ct);
                return ToHttpResult(result, v => new RealignmentResponse(v.RealignmentId, v.Status));
            })
            .Produces<RealignmentResponse>()
            .WithTags("PortfolioEndpoints");
    }
}
