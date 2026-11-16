using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.PublicApi.Portfolio.Services;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 4 — realign every portfolio that follows a model (operator only).</summary>
public class TriggerRealignmentEndpoint : IEndpoint<IResult, TriggerRealignmentRequest, IRealignmentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/models/{modelId}/realignments",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string modelId, IRealignmentService service, HttpContext http, CancellationToken ct) =>
            {
                return await HandleAsync(new TriggerRealignmentRequest { ModelId = modelId, CallerId = http.User.ShopperId(), Cancellation = ct }, service);
            })
            .Produces<RealignmentResponse>(StatusCodes.Status202Accepted)
            .WithTags("PortfolioEndpoints");
    }

    public Task<IResult> HandleAsync(TriggerRealignmentRequest request, IRealignmentService service) =>
        EndpointSupport.Guarded(async () =>
        {
            var result = await service.TriggerAsync(request.ModelId, request.Cancellation);
            return Results.Accepted($"api/portfolio/realignments/{result.RealignmentId}", new RealignmentResponse
            {
                RealignmentId = result.RealignmentId,
                Status = StatusText.For(result.State),
            });
        });
}

/// <summary>Flow 4 — where a realignment has got to.</summary>
public class GetRealignmentEndpoint : IEndpoint<IResult, GetRealignmentRequest, IRealignmentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/realignments/{realignmentId:guid}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (Guid realignmentId, IRealignmentService service, CancellationToken ct) =>
            {
                return await HandleAsync(new GetRealignmentRequest { RealignmentId = realignmentId, Cancellation = ct }, service);
            })
            .Produces<RealignmentResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("PortfolioEndpoints");
    }

    public Task<IResult> HandleAsync(GetRealignmentRequest request, IRealignmentService service) =>
        EndpointSupport.Guarded(async () =>
        {
            var result = await service.GetAsync(request.RealignmentId, request.Cancellation);
            if (result is null)
                return Results.NotFound();
            return Results.Ok(new RealignmentResponse
            {
                RealignmentId = result.RealignmentId,
                Status = StatusText.For(result.State),
            });
        });
}
