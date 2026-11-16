using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.Extensions.Logging;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 2 — operator creates or replaces a model portfolio (admin only).</summary>
public sealed class UpsertModelEndpoint : PortfolioEndpointBase, IEndpoint<IResult, string, UpsertModelRequest>
{
    private static readonly Regex IsinPattern = new("^[A-Z]{2}[A-Z0-9]{9}[0-9]$", RegexOptions.Compiled);

    public UpsertModelEndpoint(
        IUpvestPortfolioGateway gateway, IPortfolioStore store, IHttpContextAccessor http,
        ILogger<UpsertModelEndpoint> logger) : base(gateway, store, http, logger) { }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/models/{modelId}",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (string modelId, UpsertModelRequest request) => await HandleAsync(modelId, request))
            .Produces<ModelResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(string modelId, UpsertModelRequest request)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return Results.BadRequest(new { error = "modelId is required." });
        }

        if (!Validate(request, out var funds, out var error))
        {
            return Results.BadRequest(new { error });
        }

        var existing = Store.GetModel(modelId);

        try
        {
            ModelPortfolioResult result;
            if (existing?.AllocationId is { } allocationId)
            {
                result = await Gateway.ReplaceModelAsync(allocationId, request.Name.Trim(), funds, Ct);
            }
            else
            {
                result = await Gateway.CreateModelAsync(request.Name.Trim(), funds, Guid.NewGuid(), Ct);
            }

            var model = existing ?? new ModelPortfolio(modelId, result.Name, MapFunds(result.Funds));
            model.Replace(result.Name, MapFunds(result.Funds));
            model.AllocationId = result.AllocationId;
            Store.UpsertModel(model);

            return Results.Ok(new ModelResponse
            {
                ModelId = modelId,
                Name = result.Name,
                Funds = result.Funds.Select(f => new ModelFundDto { InstrumentId = f.InstrumentId, Weight = f.Weight }).ToList(),
            });
        }
        catch (UpvestGatewayException ex)
        {
            Logger.LogWarning("Model upsert failed for '{ModelId}' (status {Status}).", modelId, ex.StatusCode);
            return PortfolioShared.ToProblem(ex);
        }
    }

    private static IReadOnlyList<ModelPortfolioFund> MapFunds(IReadOnlyList<FundWeight> funds) =>
        funds.Select(f => new ModelPortfolioFund(f.InstrumentId, f.Weight)).ToList();

    private static bool Validate(UpsertModelRequest r, out IReadOnlyList<FundWeight> funds, out string error)
    {
        funds = Array.Empty<FundWeight>();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(r.Name)) { error = "name is required."; return false; }
        if (r.Funds is null || r.Funds.Count == 0) { error = "funds must contain at least one entry."; return false; }

        decimal sum = 0m;
        var built = new List<FundWeight>(r.Funds.Count);
        foreach (var f in r.Funds)
        {
            var isin = f.InstrumentId?.Trim().ToUpperInvariant() ?? string.Empty;
            if (!IsinPattern.IsMatch(isin)) { error = $"instrumentId '{isin}' is not a valid ISIN."; return false; }
            if (f.Weight <= 0m || f.Weight > 100m) { error = "each fund weight must be between 0 (exclusive) and 100."; return false; }
            sum += f.Weight;
            built.Add(new FundWeight(isin, f.Weight));
        }

        if (sum != 100m) { error = "fund weights must sum to exactly 100."; return false; }

        funds = built;
        return true;
    }
}

/// <summary>Flow 2 — list every model portfolio and its funds (any signed-in shopper).</summary>
public sealed class ListModelsEndpoint : PortfolioEndpointBase, IEndpoint<IResult>
{
    public ListModelsEndpoint(
        IUpvestPortfolioGateway gateway, IPortfolioStore store, IHttpContextAccessor http,
        ILogger<ListModelsEndpoint> logger) : base(gateway, store, http, logger) { }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/models",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            () => HandleAsync())
            .Produces<ModelListResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public Task<IResult> HandleAsync()
    {
        var models = Store.ListModels()
            .Select(m => new ModelResponse
            {
                ModelId = m.ModelId,
                Name = m.Name,
                Funds = m.Funds.Select(f => new ModelFundDto { InstrumentId = f.InstrumentId, Weight = f.Weight }).ToList(),
            })
            .ToList();

        return Task.FromResult<IResult>(Results.Ok(new ModelListResponse { Models = models }));
    }
}

/// <summary>Flow 2 — the signed-in investor chooses which model their portfolio follows.</summary>
public sealed class ChooseModelEndpoint : PortfolioEndpointBase, IEndpoint<IResult, ChooseModelRequest>
{
    public ChooseModelEndpoint(
        IUpvestPortfolioGateway gateway, IPortfolioStore store, IHttpContextAccessor http,
        ILogger<ChooseModelEndpoint> logger) : base(gateway, store, http, logger) { }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/investor/model",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ChooseModelRequest request) => await HandleAsync(request))
            .Produces<ChooseModelResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(ChooseModelRequest request)
    {
        var buyerId = BuyerId;
        if (buyerId is null)
        {
            return Results.Unauthorized();
        }

        if (request is null || string.IsNullOrWhiteSpace(request.ModelId))
        {
            return Results.BadRequest(new { error = "modelId is required." });
        }

        var investor = Store.GetInvestor(buyerId);
        if (investor is null || investor.UpvestUserId is null)
        {
            return Results.NotFound(new { error = "You are not registered as an investor." });
        }

        // Cross-operation invariant: the chosen model must be one the operator offers (an existing allocation).
        var model = Store.GetModel(request.ModelId);
        if (model?.AllocationId is not { } allocationId)
        {
            return Results.BadRequest(new { error = $"Model '{request.ModelId}' does not exist." });
        }

        if (investor.AccountId is not { } accountId)
        {
            return Results.Conflict(new { error = "Your investor account is not ready yet." });
        }

        try
        {
            var alreadyConfigured = investor.SelectedModelId is not null;
            await Gateway.SetInvestorConfigurationAsync(accountId, allocationId, Guid.NewGuid(), alreadyConfigured, Ct);

            investor.SelectedModelId = request.ModelId;
            Store.UpdateInvestor(investor);

            return Results.Ok(new ChooseModelResponse { ModelId = request.ModelId });
        }
        catch (UpvestGatewayException ex)
        {
            Logger.LogWarning("Choosing model '{ModelId}' failed (status {Status}).", request.ModelId, ex.StatusCode);
            return PortfolioShared.ToProblem(ex);
        }
    }
}
