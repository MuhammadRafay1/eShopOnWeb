using System;
using System.Collections.Generic;
using System.Linq;
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

/// <summary>PUT /api/portfolio/models/{modelId} — operator creates or replaces a model portfolio.</summary>
public class UpsertModelEndpoint : IEndpoint<IResult, UpsertModelRequest, IUpvestPortfolioService>
{
    private readonly IHttpContextAccessor _http;
    public UpsertModelEndpoint(IHttpContextAccessor http) => _http = http;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/models/{modelId}",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
                AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (UpsertModelRequest request, IUpvestPortfolioService service) => await HandleAsync(request, service))
            .Produces<ModelResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(UpsertModelRequest request, IUpvestPortfolioService service)
    {
        var ctx = _http.HttpContext!;
        var modelId = ctx.Request.RouteValues["modelId"]?.ToString();
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return Results.BadRequest(new { error = "A modelId path value is required." });
        }
        if (request is null || request.Funds is null || request.Funds.Count == 0 || string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { error = "A model requires a name and at least one fund." });
        }

        var definition = new ModelPortfolioDefinition(
            request.Name,
            request.Funds.Select(f => new ModelFundInput(f.InstrumentId, f.Weight)).ToList());

        try
        {
            var result = await service.UpsertModelAsync(modelId, definition, ctx.RequestAborted);
            return Results.Ok(ToResponse(result));
        }
        catch (Exception ex)
        {
            return TryMapError(ex) ?? throw ex;
        }
    }

    internal static ModelResponse ToResponse(ModelPortfolioResult result) => new()
    {
        ModelId = result.ModelId,
        Name = result.Name,
        Funds = result.Funds.Select(f => new ModelFundDto { InstrumentId = f.InstrumentId, Weight = f.Weight }).ToList()
    };
}

/// <summary>GET /api/portfolio/models — every model portfolio and its funds.</summary>
public class ListModelsEndpoint : IEndpoint<IResult, IUpvestPortfolioService>
{
    private readonly IHttpContextAccessor _http;
    public ListModelsEndpoint(IHttpContextAccessor http) => _http = http;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/models",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (IUpvestPortfolioService service) => await HandleAsync(service))
            .Produces<List<ModelResponse>>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(IUpvestPortfolioService service)
    {
        try
        {
            var models = await service.ListModelsAsync(_http.HttpContext!.RequestAborted);
            return Results.Ok(models.Select(UpsertModelEndpoint.ToResponse).ToList());
        }
        catch (Exception ex)
        {
            return TryMapError(ex) ?? throw ex;
        }
    }
}
