using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class ModelPortfolioService : IModelPortfolioService
{
    private readonly IRepository<ModelPortfolio> _models;
    private readonly IRepository<Investor> _investors;
    private readonly IUpvestClient _upvest;
    private readonly IAppLogger<ModelPortfolioService> _logger;

    public ModelPortfolioService(
        IRepository<ModelPortfolio> models,
        IRepository<Investor> investors,
        IUpvestClient upvest,
        IAppLogger<ModelPortfolioService> logger)
    {
        _models = models;
        _investors = investors;
        _upvest = upvest;
        _logger = logger;
    }

    public async Task<ModelPortfolio> UpsertAsync(string modelId, string name, IEnumerable<ModelFund> funds, CancellationToken ct = default)
    {
        Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        var fundList = funds.ToList();

        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), ct);
        bool isNew = model is null;
        if (model is null)
            model = new ModelPortfolio(modelId, name, fundList);
        else
            model.Replace(name, fundList);

        // Mirror the model to an Upvest allocation (the backing resource).
        try
        {
            UpvestAllocation allocation = model.UpvestAllocationId is null
                ? await _upvest.CreateAllocationAsync(name, fundList, ct)
                : await _upvest.UpdateAllocationAsync(model.UpvestAllocationId, name, fundList, ct);
            model.SetUpvestAllocationId(allocation.Id);
        }
        catch (UpvestException ex) when (ex.IsScopeAuthorizationFailure)
        {
            // Known environment limitation: the sandbox client cannot use portfolios:admin. Keep the
            // operator's definition locally so the model is listable and selectable; the allocation stays unsynced.
            _logger.LogWarning("Model '{ModelId}' saved locally; Upvest allocation not synced (HTTP {Status}: {Detail}).",
                modelId, ex.StatusCode, ex.Message);
        }

        if (isNew) await _models.AddAsync(model, ct);
        else await _models.UpdateAsync(model, ct);

        return model;
    }

    public async Task<IReadOnlyList<ModelPortfolio>> ListAsync(CancellationToken ct = default)
    {
        return await _models.ListAsync(ct);
    }

    public async Task ChooseModelAsync(string shopUserName, string modelId, CancellationToken ct = default)
    {
        Guard.Against.NullOrEmpty(modelId, nameof(modelId));

        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopUserSpecification(shopUserName), ct)
            ?? throw new InvestorNotFoundException("You must sign up as an investor before choosing a model.");

        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), ct)
            ?? throw new ModelNotFoundException($"Model portfolio '{modelId}' does not exist.");

        // Link the investor's account to the model's allocation at Upvest (follow the model).
        if (investor.AccountId is not null && model.UpvestAllocationId is not null)
        {
            try
            {
                await _upvest.SetConfigurationAsync(investor.AccountId, model.UpvestAllocationId, ct);
            }
            catch (UpvestException ex) when (ex.IsScopeAuthorizationFailure)
            {
                _logger.LogWarning("Model choice stored locally; Upvest configuration not synced (HTTP {Status}: {Detail}).",
                    ex.StatusCode, ex.Message);
            }
        }

        investor.ChooseModel(modelId);
        await _investors.UpdateAsync(investor, ct);
    }
}
