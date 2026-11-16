using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

public sealed class RealignRequest
{
    public string ModelId { get; set; } = string.Empty;
    public CancellationToken CancellationToken { get; set; }
}

/// <summary>
/// POST /api/portfolio/models/{modelId}/realignments — operator action: realign every portfolio
/// following the model back to its current weights.
/// </summary>
public sealed class RealignEndpoint : IEndpoint<IResult, RealignRequest, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/models/{modelId}/realignments",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (string modelId, HttpContext http, IPortfolioService service) =>
                await HandleAsync(new RealignRequest { ModelId = modelId, CancellationToken = http.RequestAborted }, service))
            .Produces<RealignmentResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(RealignRequest request, IPortfolioService service)
    {
        if (string.IsNullOrWhiteSpace(request.ModelId))
            throw PortfolioException.BadRequest("modelId is required.");

        var result = await service.RealignAsync(request.ModelId, request.CancellationToken);
        return Results.Ok(result.ToResponse());
    }
}

public sealed class GetRealignmentRequest
{
    public string RealignmentId { get; set; } = string.Empty;
    public CancellationToken CancellationToken { get; set; }
}

/// <summary>
/// GET /api/portfolio/realignments/{realignmentId} — reports where a realignment has got to.
/// </summary>
public sealed class GetRealignmentEndpoint : IEndpoint<IResult, GetRealignmentRequest, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/realignments/{realignmentId}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (string realignmentId, HttpContext http, IPortfolioService service) =>
                await HandleAsync(new GetRealignmentRequest { RealignmentId = realignmentId, CancellationToken = http.RequestAborted }, service))
            .Produces<RealignmentResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(GetRealignmentRequest request, IPortfolioService service)
    {
        var result = await service.GetRealignmentAsync(request.RealignmentId, request.CancellationToken);
        return result is null
            ? Results.NotFound()
            : Results.Ok(result.ToResponse());
    }
}
