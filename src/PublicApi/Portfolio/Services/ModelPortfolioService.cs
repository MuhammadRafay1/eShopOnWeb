using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Services;

public interface IModelPortfolioService
{
    Task<ModelResult> UpsertAsync(string modelId, string name, IReadOnlyList<UpvestFund> funds, CancellationToken ct);
    Task<IReadOnlyList<ModelResult>> ListAsync(CancellationToken ct);
    Task SelectAsync(string shopperId, string modelId, CancellationToken ct);
}

/// <summary>
/// Operator model-portfolio management (create/replace/list) plus an investor's choice of model.
/// A model is mirrored at Upvest as an allocation; the investor's choice is a portfolio configuration.
/// </summary>
public sealed class ModelPortfolioService : IModelPortfolioService
{
    private readonly IRepository<ModelPortfolio> _models;
    private readonly IRepository<Investor> _investors;
    private readonly IUpvestInvestmentGateway _gateway;
    private readonly ILogger<ModelPortfolioService> _logger;

    public ModelPortfolioService(
        IRepository<ModelPortfolio> models,
        IRepository<Investor> investors,
        IUpvestInvestmentGateway gateway,
        ILogger<ModelPortfolioService> logger)
    {
        _models = models;
        _investors = investors;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<ModelResult> UpsertAsync(string modelId, string name, IReadOnlyList<UpvestFund> funds, CancellationToken ct)
    {
        ValidateFunds(funds);

        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), ct);
        var modelFunds = funds.Select(f => new ModelFund(f.InstrumentId, f.Weight)).ToList();

        if (model is null)
        {
            // Create the app model first (claim), then the Upvest allocation, then store its id.
            model = new ModelPortfolio(modelId, name, modelFunds);
            model = await _models.AddAsync(model, ct);

            var allocationId = await _gateway.CreateAllocationAsync(name, funds, model.AllocationCreateKey, ct);
            model.SetUpvestAllocationId(allocationId);
            await _models.UpdateAsync(model, ct);
        }
        else
        {
            model.Replace(name, modelFunds);
            if (model.UpvestAllocationId.HasValue)
            {
                await _gateway.UpdateAllocationAsync(model.UpvestAllocationId.Value, name, funds, ct);
            }
            else
            {
                // A prior create never got its allocation id; create it now.
                var allocationId = await _gateway.CreateAllocationAsync(name, funds, model.AllocationCreateKey, ct);
                model.SetUpvestAllocationId(allocationId);
            }
            await _models.UpdateAsync(model, ct);
        }

        return ToResult(model);
    }

    public async Task<IReadOnlyList<ModelResult>> ListAsync(CancellationToken ct)
    {
        var models = await _models.ListAsync(new ModelPortfoliosWithFundsSpecification(), ct);
        return models.Select(ToResult).ToList();
    }

    public async Task SelectAsync(string shopperId, string modelId, CancellationToken ct)
    {
        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), ct);
        if (model is null)
            throw new PortfolioValidationException($"Model '{modelId}' does not exist.", 404);
        if (!model.UpvestAllocationId.HasValue)
            throw new PortfolioValidationException($"Model '{modelId}' is not yet available at the investment provider.", 409);

        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperIdSpecification(shopperId), ct);
        if (investor is null || !investor.UpvestAccountId.HasValue)
            throw new PortfolioValidationException("You must be an accepted investor with a ready account before choosing a model.", 409);

        var isSwitch = investor.SelectedAllocationId.HasValue;
        await _gateway.SetConfigurationAsync(investor.UpvestAccountId.Value, model.UpvestAllocationId.Value, isSwitch, Guid.NewGuid(), ct);

        investor.SelectModel(model.ModelId, model.UpvestAllocationId.Value);
        await _investors.UpdateAsync(investor, ct);
        _logger.LogInformation("Investor chose model {ModelId}", modelId);
    }

    private static void ValidateFunds(IReadOnlyList<UpvestFund> funds)
    {
        if (funds is null || funds.Count == 0)
            throw new PortfolioValidationException("A model must have at least one fund.");
        if (funds.Any(f => string.IsNullOrWhiteSpace(f.InstrumentId)))
            throw new PortfolioValidationException("Every fund must have an instrumentId (ISIN).");
        if (funds.Any(f => f.Weight < 0))
            throw new PortfolioValidationException("Fund weights cannot be negative.");
        var total = funds.Sum(f => f.Weight);
        if (total != 100m)
            throw new PortfolioValidationException($"Fund weights must add up to exactly 100 (got {total}).");
    }

    private static ModelResult ToResult(ModelPortfolio model) =>
        new(model.ModelId, model.Name, model.Funds.Select(f => new UpvestFund(f.InstrumentId, f.Weight)).ToList());
}
