using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Operator action: realign every investor who follows a model to its current weights.</summary>
public class TriggerRealignmentEndpoint : IEndpoint<IResult, TriggerRealignmentCommand, IManagedPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/models/{modelId}/realignments",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string modelId, IManagedPortfolioService service) =>
                await HandleAsync(new TriggerRealignmentCommand(modelId), service))
            .Produces<RealignmentResponse>(StatusCodes.Status202Accepted)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(TriggerRealignmentCommand command, IManagedPortfolioService service)
    {
        var realignment = await service.TriggerRealignmentAsync(command.ModelId);
        return Results.Accepted($"api/portfolio/realignments/{realignment.PublicId}", realignment.ToResponse());
    }
}

/// <summary>Reports where a realignment has got to at Upvest.</summary>
public class GetRealignmentEndpoint : IEndpoint<IResult, GetRealignmentCommand, IManagedPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/realignments/{realignmentId}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string realignmentId, IManagedPortfolioService service) =>
                await HandleAsync(new GetRealignmentCommand(realignmentId), service))
            .Produces<RealignmentResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(GetRealignmentCommand command, IManagedPortfolioService service)
    {
        var realignment = await service.GetRealignmentAsync(command.RealignmentId);
        return realignment is null
            ? Results.NotFound($"No realignment with id '{command.RealignmentId}' exists.")
            : Results.Ok(realignment.ToResponse());
    }
}
