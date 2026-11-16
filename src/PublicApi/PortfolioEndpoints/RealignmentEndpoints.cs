using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;
using MinimalApi.Endpoint;
using static Microsoft.eShopWeb.PublicApi.PortfolioEndpoints.PortfolioEndpointSupport;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>POST /api/portfolio/models/{modelId}/realignments — operator realigns everyone on a model.</summary>
public class TriggerRealignmentEndpoint : IEndpoint<IResult, IUpvestPortfolioService>
{
    private readonly IHttpContextAccessor _http;
    public TriggerRealignmentEndpoint(IHttpContextAccessor http) => _http = http;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/models/{modelId}/realignments",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
                AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (IUpvestPortfolioService service) => await HandleAsync(service))
            .Produces<RealignmentResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(IUpvestPortfolioService service)
    {
        var ctx = _http.HttpContext!;
        var modelId = ctx.Request.RouteValues["modelId"]?.ToString();
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return Results.BadRequest(new { error = "A modelId path value is required." });
        }

        try
        {
            var result = await service.RealignAsync(modelId, ctx.RequestAborted);
            return Results.Ok(new RealignmentResponse
            {
                RealignmentId = result.RealignmentId,
                Status = PortfolioStatusText.For(result.Status)
            });
        }
        catch (Exception ex)
        {
            return TryMapError(ex) ?? throw ex;
        }
    }
}

/// <summary>GET /api/portfolio/realignments/{realignmentId} — status of a realignment.</summary>
public class GetRealignmentEndpoint : IEndpoint<IResult, IUpvestPortfolioService>
{
    private readonly IHttpContextAccessor _http;
    public GetRealignmentEndpoint(IHttpContextAccessor http) => _http = http;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/realignments/{realignmentId}",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
                AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (IUpvestPortfolioService service) => await HandleAsync(service))
            .Produces<RealignmentResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(IUpvestPortfolioService service)
    {
        var ctx = _http.HttpContext!;
        var raw = ctx.Request.RouteValues["realignmentId"]?.ToString();
        if (!Guid.TryParse(raw, out var realignmentId))
        {
            return Results.BadRequest(new { error = "A valid realignmentId is required." });
        }

        try
        {
            var result = await service.GetRealignmentAsync(realignmentId, ctx.RequestAborted);
            return Results.Ok(new RealignmentResponse
            {
                RealignmentId = result.RealignmentId,
                Status = PortfolioStatusText.For(result.Status)
            });
        }
        catch (Exception ex)
        {
            return TryMapError(ex) ?? throw ex;
        }
    }
}
