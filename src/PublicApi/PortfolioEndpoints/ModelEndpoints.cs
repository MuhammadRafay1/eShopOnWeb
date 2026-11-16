using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

public sealed class UpsertModelRequestEnvelope
{
    public string ModelId { get; set; } = string.Empty;
    public UpsertModelRequest Body { get; set; } = default!;
    public CancellationToken CancellationToken { get; set; }
}

/// <summary>
/// PUT /api/portfolio/models/{modelId} — operator action: create or replace a model portfolio.
/// </summary>
public sealed class UpsertModelEndpoint : IEndpoint<IResult, UpsertModelRequestEnvelope, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/models/{modelId}",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (string modelId, UpsertModelRequest body, HttpContext http, IPortfolioService service) =>
                await HandleAsync(new UpsertModelRequestEnvelope { ModelId = modelId, Body = body, CancellationToken = http.RequestAborted }, service))
            .Produces<ModelResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(UpsertModelRequestEnvelope request, IPortfolioService service)
    {
        if (request.Body is null)
            throw PortfolioException.BadRequest("A request body is required.");
        if (string.IsNullOrWhiteSpace(request.ModelId))
            throw PortfolioException.BadRequest("modelId is required.");

        var funds = (request.Body.Funds ?? new List<ModelFundDto>())
            .Select(f => new ModelFund(f.InstrumentId?.Trim() ?? string.Empty, f.Weight))
            .ToList();

        var model = await service.UpsertModelAsync(request.ModelId, request.Body.Name ?? string.Empty, funds, request.CancellationToken);
        return Results.Ok(model.ToResponse());
    }
}

public sealed class ListModelsRequest
{
    public CancellationToken CancellationToken { get; set; }
}

/// <summary>
/// GET /api/portfolio/models — lists every model portfolio and its funds.
/// </summary>
public sealed class ListModelsEndpoint : IEndpoint<IResult, ListModelsRequest, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/models",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (HttpContext http, IPortfolioService service) =>
                await HandleAsync(new ListModelsRequest { CancellationToken = http.RequestAborted }, service))
            .Produces<ModelListResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(ListModelsRequest request, IPortfolioService service)
    {
        var models = await service.GetModelsAsync(request.CancellationToken);
        return Results.Ok(new ModelListResponse(models.Select(m => m.ToResponse()).ToList()));
    }
}

public sealed class ChooseModelRequestEnvelope : ShopperRequest
{
    public ChooseModelRequest Body { get; set; } = default!;
}

/// <summary>
/// PUT /api/portfolio/investor/model — the signed-in investor chooses which model to follow.
/// </summary>
public sealed class ChooseModelEndpoint : IEndpoint<IResult, ChooseModelRequestEnvelope, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/investor/model",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ChooseModelRequest body, HttpContext http, IPortfolioService service) =>
                await HandleAsync(new ChooseModelRequestEnvelope
                {
                    Body = body,
                    ShopperId = PortfolioEndpointHelpers.ShopperId(http),
                    CancellationToken = http.RequestAborted
                }, service))
            .Produces(StatusCodes.Status204NoContent)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(ChooseModelRequestEnvelope request, IPortfolioService service)
    {
        if (request.Body is null || string.IsNullOrWhiteSpace(request.Body.ModelId))
            throw PortfolioException.BadRequest("modelId is required.");

        await service.ChooseModelAsync(request.ShopperId, request.Body.ModelId!.Trim(), request.CancellationToken);
        return Results.NoContent();
    }
}
