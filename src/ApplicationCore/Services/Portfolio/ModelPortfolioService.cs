using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services.Portfolio;

public class ModelPortfolioService : IModelPortfolioService
{
    private readonly IPortfolioRepository<ModelPortfolio> _models;
    private readonly IUpvestInvestmentGateway _gateway;
    private readonly IAppLogger<ModelPortfolioService> _logger;

    public ModelPortfolioService(
        IPortfolioRepository<ModelPortfolio> models,
        IUpvestInvestmentGateway gateway,
        IAppLogger<ModelPortfolioService> logger)
    {
        _models = models;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task PutModelAsync(string modelId, string name, IReadOnlyList<FundWeight> funds, CancellationToken cancellationToken)
    {
        // Cross-operation invariant: every fund ISIN written into a model must be a real Upvest
        // instrument. Validate before the write where the instrument catalogue is reachable; if the
        // client is not authorised to read instruments (scope wall), skip validation with a warning
        // rather than refusing every model in this environment — the weights/shape are still checked.
        await ValidateInstrumentsAsync(funds, cancellationToken);

        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByIdSpec(modelId), cancellationToken);
        var isNew = model is null;

        var fundEntities = funds.Select(f => new ModelFund(f.InstrumentId, f.Weight)).ToList();

        if (model is null)
        {
            model = new ModelPortfolio(modelId, name, fundEntities); // throws if weights != 100
            await _models.AddAsync(model, cancellationToken);
        }
        else
        {
            model.Replace(name, fundEntities); // throws if weights != 100
            await _models.UpdateAsync(model, cancellationToken);
        }

        // Reflect the model at Upvest as a portfolio allocation (create on first PUT, replace after).
        try
        {
            if (model.UpvestAllocationId is null)
            {
                var allocationId = await _gateway.CreateAllocationAsync(name, funds, model.IdempotencyKey, cancellationToken);
                model.SetAllocationId(allocationId);
                await _models.UpdateAsync(model, cancellationToken);
            }
            else
            {
                await _gateway.UpdateAllocationAsync(model.UpvestAllocationId.Value, name, funds, cancellationToken);
            }
        }
        catch (UpvestGatewayException ex) when (ex.IsScopeWall || ex.OutcomeUnknown)
        {
            _logger.LogWarning("Model '{ModelId}' saved; Upvest allocation deferred (scopeWall={ScopeWall}, unknown={Unknown}, new={IsNew}).",
                modelId, ex.IsScopeWall, ex.OutcomeUnknown, isNew);
        }
    }

    public async Task<IReadOnlyList<ModelSummary>> ListAsync(CancellationToken cancellationToken)
    {
        var models = await _models.ListAsync(new ModelPortfoliosSpec(), cancellationToken);
        return models
            .Select(m => new ModelSummary(
                m.ModelId,
                m.Name,
                m.Funds.Select(f => new FundWeight(f.InstrumentId, f.Weight)).ToList()))
            .ToList();
    }

    private async Task ValidateInstrumentsAsync(IReadOnlyList<FundWeight> funds, CancellationToken cancellationToken)
    {
        foreach (var fund in funds)
        {
            try
            {
                if (!await _gateway.InstrumentExistsAsync(fund.InstrumentId, cancellationToken))
                {
                    throw new ArgumentException($"'{fund.InstrumentId}' is not a known Upvest instrument (ISIN).", nameof(funds));
                }
            }
            catch (UpvestGatewayException ex) when (ex.IsScopeWall)
            {
                _logger.LogWarning("Skipping instrument validation — client is not authorised to read instruments.");
                return;
            }
        }
    }
}
