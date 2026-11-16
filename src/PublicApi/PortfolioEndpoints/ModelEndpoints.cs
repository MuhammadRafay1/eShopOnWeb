using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>PUT /api/portfolio/models/{modelId} — operator action: create or replace a model portfolio.</summary>
public class UpsertModelEndpoint : IEndpoint<IResult, UpsertModelRequest, IModelPortfolioService>
{
    private readonly IHttpContextAccessor _http;
    public UpsertModelEndpoint(IHttpContextAccessor http) => _http = http;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/models/{modelId}",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (UpsertModelRequest request, IModelPortfolioService service) => await HandleAsync(request, service))
            .Produces<ModelDto>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(UpsertModelRequest request, IModelPortfolioService service)
    {
        var ctx = _http.HttpContext!;
        var modelId = ctx.Request.RouteValues["modelId"]?.ToString();
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return Results.BadRequest(new { message = "A modelId is required in the route." });
        }
        if (request is null || string.IsNullOrWhiteSpace(request.Name) || request.Funds is null || request.Funds.Count == 0)
        {
            return Results.BadRequest(new { message = "A model name and at least one fund are required." });
        }

        var funds = request.Funds.Select(f => new FundWeight(f.InstrumentId, f.Weight)).ToList();
        var view = await service.UpsertModelAsync(modelId, request.Name, funds, ctx.RequestAborted);
        return Results.Ok(ToDto(view));
    }

    internal static ModelDto ToDto(ModelView view) => new()
    {
        ModelId = view.ModelId,
        Name = view.Name,
        Funds = view.Funds.Select(f => new FundDto { InstrumentId = f.InstrumentId, Weight = f.Weight }).ToList()
    };
}

/// <summary>GET /api/portfolio/models — every model portfolio and its funds.</summary>
public class ListModelsEndpoint : IEndpoint<IResult, IModelPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/models",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IModelPortfolioService service) => await HandleAsync(service))
            .Produces<List<ModelDto>>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(IModelPortfolioService service)
    {
        var models = await service.ListModelsAsync(default);
        return Results.Ok(models.Select(UpsertModelEndpoint.ToDto).ToList());
    }
}

/// <summary>PUT /api/portfolio/investor/model — the signed-in investor chooses which model they follow.</summary>
public class ChooseModelEndpoint : IEndpoint<IResult, ChooseModelRequest, IModelPortfolioService>
{
    private readonly IHttpContextAccessor _http;
    public ChooseModelEndpoint(IHttpContextAccessor http) => _http = http;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/investor/model",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ChooseModelRequest request, IModelPortfolioService service) => await HandleAsync(request, service))
            .Produces(StatusCodes.Status204NoContent)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(ChooseModelRequest request, IModelPortfolioService service)
    {
        var ctx = _http.HttpContext!;
        var userName = PortfolioHttp.UserName(ctx);
        if (string.IsNullOrEmpty(userName))
        {
            return Results.Unauthorized();
        }
        if (request is null || string.IsNullOrWhiteSpace(request.ModelId))
        {
            return Results.BadRequest(new { message = "A modelId is required." });
        }

        await service.ChooseModelAsync(userName, request.ModelId, ctx.RequestAborted);
        return Results.NoContent();
    }
}
