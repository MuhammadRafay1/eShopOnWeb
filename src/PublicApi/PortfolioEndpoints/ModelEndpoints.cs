using System;
using System.Collections.Generic;
using System.Linq;
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
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// PUT /api/portfolio/models/{modelId} — create or replace a model portfolio (operator only).
/// </summary>
public class UpsertModelEndpoint : IEndpoint<IResult, UpsertModelRequest, HttpContext>
{
    private readonly IUpvestInvestmentService _service;
    private readonly IRepository<ModelPortfolio> _models;
    private readonly IAppLogger<UpsertModelEndpoint> _logger;

    public UpsertModelEndpoint(IUpvestInvestmentService service, IRepository<ModelPortfolio> models, IAppLogger<UpsertModelEndpoint> logger)
    {
        _service = service;
        _models = models;
        _logger = logger;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/models/{modelId}",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string modelId, UpsertModelRequest request, HttpContext http, CancellationToken ct) =>
            {
                return await HandleAsync(request, http, ct);
            })
            .Produces<ModelDto>()
            .WithTags("PortfolioEndpoints");
    }

    public Task<IResult> HandleAsync(UpsertModelRequest request, HttpContext http) => HandleAsync(request, http, default);

    public async Task<IResult> HandleAsync(UpsertModelRequest request, HttpContext http, CancellationToken ct)
    {
        var modelId = (string?)http.Request.RouteValues["modelId"];
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return Results.BadRequest(new { message = "A model id is required." });
        }
        if (request.Funds is null || request.Funds.Count == 0)
        {
            return Results.BadRequest(new { message = "At least one fund is required." });
        }

        var total = request.Funds.Sum(f => f.Weight);
        if (total != 100m)
        {
            return Results.BadRequest(new { message = $"Fund weights must sum to exactly 100, but summed to {total}." });
        }

        var funds = request.Funds.Select(f => new ModelFund(f.InstrumentId, f.Weight)).ToList();
        var fundInputs = request.Funds.Select(f => new ModelFundInput(f.InstrumentId, f.Weight)).ToList();

        // Validate each fund ISIN against Upvest's tradable instruments. Degrades if the sandbox refuses the scope.
        try
        {
            var unknown = await _service.FindUnknownInstrumentsAsync(request.Funds.Select(f => f.InstrumentId).ToList(), ct);
            if (unknown.Count > 0)
            {
                return Results.BadRequest(new { message = $"Unknown instrument ISIN(s): {string.Join(", ", unknown)}." });
            }
        }
        catch (UpvestIntegrationException ex) when (ex.Kind == UpvestFailureKind.ProviderRejectedUs)
        {
            _logger.LogWarning("Instrument validation skipped (status {Status}): client not authorised for the instruments scope.", ex.StatusCode ?? 0);
        }

        var existing = await _models.GetByIdAsync(modelId, ct);

        // Create or replace the Upvest allocation backing this model. In the sandbox the portfolios scope is
        // refused, so the model is still defined app-side (operator's catalog) with no allocation link yet.
        Guid? allocationId = existing?.UpvestAllocationId;
        try
        {
            allocationId = await _service.UpsertModelAllocationAsync(request.Name, fundInputs, existing?.UpvestAllocationId, Guid.NewGuid(), ct);
        }
        catch (UpvestIntegrationException ex) when (ex.Kind == UpvestFailureKind.ProviderRejectedUs)
        {
            _logger.LogWarning("Model allocation not created at Upvest (status {Status}): client not authorised for the portfolios scope. Model defined app-side only.", ex.StatusCode ?? 0);
        }

        bool created = existing is null;
        if (existing is null)
        {
            existing = new ModelPortfolio(modelId, request.Name, funds);
        }
        else
        {
            existing.Replace(request.Name, funds);
        }
        if (allocationId is { } id)
        {
            existing.LinkAllocation(id);
        }

        if (created)
        {
            await _models.AddAsync(existing, ct);
        }
        else
        {
            await _models.UpdateAsync(existing, ct);
        }

        var dto = ToDto(existing);
        return created
            ? Results.Created($"api/portfolio/models/{modelId}", dto)
            : Results.Ok(dto);
    }

    internal static ModelDto ToDto(ModelPortfolio model) => new()
    {
        ModelId = model.ModelId,
        Name = model.Name,
        Funds = model.Funds.Select(f => new FundDto { InstrumentId = f.InstrumentId, Weight = f.Weight }).ToList()
    };
}

/// <summary>
/// GET /api/portfolio/models — every model portfolio and its funds.
/// </summary>
public class ListModelsEndpoint : IEndpoint<IResult, IRepository<ModelPortfolio>>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/models",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IRepository<ModelPortfolio> models, CancellationToken ct) =>
            {
                return await HandleAsync(models, ct);
            })
            .Produces<List<ModelDto>>()
            .WithTags("PortfolioEndpoints");
    }

    public Task<IResult> HandleAsync(IRepository<ModelPortfolio> models) => HandleAsync(models, default);

    public async Task<IResult> HandleAsync(IRepository<ModelPortfolio> models, CancellationToken ct)
    {
        var all = await models.ListAsync(ct);
        var dtos = all.Select(UpsertModelEndpoint.ToDto).ToList();
        return Results.Ok(dtos);
    }
}

/// <summary>
/// PUT /api/portfolio/investor/model — the signed-in investor chooses which model to follow.
/// </summary>
public class ChooseModelEndpoint : IEndpoint<IResult, ChooseModelRequest, HttpContext>
{
    private readonly IUpvestInvestmentService _service;
    private readonly IRepository<Investor> _investors;
    private readonly IRepository<ModelPortfolio> _models;
    private readonly IAppLogger<ChooseModelEndpoint> _logger;

    public ChooseModelEndpoint(IUpvestInvestmentService service, IRepository<Investor> investors, IRepository<ModelPortfolio> models, IAppLogger<ChooseModelEndpoint> logger)
    {
        _service = service;
        _investors = investors;
        _models = models;
        _logger = logger;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/investor/model",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ChooseModelRequest request, HttpContext http, CancellationToken ct) =>
            {
                return await HandleAsync(request, http, ct);
            })
            .WithTags("PortfolioEndpoints");
    }

    public Task<IResult> HandleAsync(ChooseModelRequest request, HttpContext http) => HandleAsync(request, http, default);

    public async Task<IResult> HandleAsync(ChooseModelRequest request, HttpContext http, CancellationToken ct)
    {
        var buyerId = PortfolioEndpointHelpers.GetBuyerId(http);
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        var investor = await _investors.GetByIdAsync(buyerId, ct);
        if (investor is null || investor.UpvestUserId is null)
        {
            return Results.NotFound(new { message = "You are not signed up as an investor." });
        }

        if (string.IsNullOrWhiteSpace(request.ModelId))
        {
            return Results.BadRequest(new { message = "A modelId is required." });
        }

        // Cross-operation invariant: the chosen model must be one the operator actually defined.
        var model = await _models.GetByIdAsync(request.ModelId, ct);
        if (model is null)
        {
            return Results.BadRequest(new { message = $"Unknown model '{request.ModelId}'." });
        }

        // Point the account at the allocation at Upvest when both exist; otherwise record the choice app-side
        // (the sandbox refuses account/portfolio scopes, so the configuration is created when scopes allow).
        bool configurationExists = investor.PortfolioConfigurationExists;
        if (investor.AccountId is { } accountId && model.UpvestAllocationId is { } allocationId)
        {
            try
            {
                await _service.FollowModelAsync(accountId, allocationId, investor.PortfolioConfigurationExists, Guid.NewGuid(), ct);
                configurationExists = true;
            }
            catch (UpvestIntegrationException ex) when (ex.Kind == UpvestFailureKind.ProviderRejectedUs)
            {
                _logger.LogWarning("Portfolio configuration not set at Upvest (status {Status}): client not authorised. Choice recorded app-side.", ex.StatusCode ?? 0);
            }
        }

        investor.FollowModel(request.ModelId, configurationExists);
        await _investors.UpdateAsync(investor, ct);

        return Results.Ok(new { modelId = request.ModelId });
    }
}
