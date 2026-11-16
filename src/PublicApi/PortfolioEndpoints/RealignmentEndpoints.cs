using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>POST /api/portfolio/models/{modelId}/realignments — operator action: realign followers.</summary>
public class TriggerRealignmentEndpoint : IEndpoint<IResult, IPortfolioRealignmentService>
{
    private readonly IHttpContextAccessor _http;
    public TriggerRealignmentEndpoint(IHttpContextAccessor http) => _http = http;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/models/{modelId}/realignments",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IPortfolioRealignmentService service) => await HandleAsync(service))
            .Produces<RealignmentResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(IPortfolioRealignmentService service)
    {
        var ctx = _http.HttpContext!;
        var modelId = ctx.Request.RouteValues["modelId"]?.ToString();
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return Results.BadRequest(new { message = "A modelId is required in the route." });
        }

        var view = await service.TriggerAsync(modelId, ctx.RequestAborted);
        return Results.Ok(new RealignmentResponse { RealignmentId = view.RealignmentId, Status = PortfolioHttp.Status(view.Status) });
    }
}

/// <summary>GET /api/portfolio/realignments/{realignmentId} — where that realignment has got to.</summary>
public class GetRealignmentEndpoint : IEndpoint<IResult, IPortfolioRealignmentService>
{
    private readonly IHttpContextAccessor _http;
    public GetRealignmentEndpoint(IHttpContextAccessor http) => _http = http;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/realignments/{realignmentId}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IPortfolioRealignmentService service) => await HandleAsync(service))
            .Produces<RealignmentResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(IPortfolioRealignmentService service)
    {
        var ctx = _http.HttpContext!;
        var raw = ctx.Request.RouteValues["realignmentId"]?.ToString();
        if (!Guid.TryParse(raw, out var realignmentId))
        {
            return Results.BadRequest(new { message = "A valid realignmentId is required." });
        }

        var view = await service.GetAsync(realignmentId, ctx.RequestAborted);
        if (view is null)
        {
            return Results.NotFound(new { message = "Unknown realignment." });
        }
        return Results.Ok(new RealignmentResponse { RealignmentId = view.RealignmentId, Status = PortfolioHttp.Status(view.Status) });
    }
}
