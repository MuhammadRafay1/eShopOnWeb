using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services.Portfolio;

public class ModelPortfolioService : IModelPortfolioService
{
    private readonly IRepository<ModelPortfolio> _models;
    private readonly IRepository<Investor> _investors;
    private readonly IUpvestInvestmentGateway _gateway;
    private readonly IAppLogger<ModelPortfolioService> _logger;

    public ModelPortfolioService(
        IRepository<ModelPortfolio> models,
        IRepository<Investor> investors,
        IUpvestInvestmentGateway gateway,
        IAppLogger<ModelPortfolioService> logger)
    {
        _models = models;
        _investors = investors;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<ModelView> UpsertModelAsync(string modelKey, string name, IReadOnlyList<FundWeight> funds, CancellationToken ct)
    {
        ValidateFunds(funds);
        await ValidateIsinsAsync(funds, ct);

        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByKeySpecification(modelKey), ct);
        var modelFunds = funds.Select(f => new ModelFund(f.InstrumentId, f.Weight)).ToList();

        if (model is null)
        {
            // Create: claim the app record first, then realize it as an Upvest allocation.
            model = new ModelPortfolio(modelKey, name, modelFunds);
            model = await _models.AddAsync(model, ct);
            var allocationId = await _gateway.CreateModelAllocationAsync(name, funds, model.AllocationIdempotencyKey, ct);
            model.SetAllocationId(allocationId);
            await _models.UpdateAsync(model, ct);
            _logger.LogInformation("Model {ModelKey} created as allocation {AllocationId}.", modelKey, allocationId);
        }
        else
        {
            model.Replace(name, modelFunds);
            if (model.UpvestAllocationId is not null)
            {
                await _gateway.UpdateModelAllocationAsync(model.UpvestAllocationId.Value, name, funds, ct);
            }
            else
            {
                // A previous create never reached Upvest; realize it now (self-heal).
                var allocationId = await _gateway.CreateModelAllocationAsync(name, funds, model.AllocationIdempotencyKey, ct);
                model.SetAllocationId(allocationId);
            }
            await _models.UpdateAsync(model, ct);
            _logger.LogInformation("Model {ModelKey} replaced (allocation {AllocationId}).", modelKey, model.UpvestAllocationId);
        }

        return ToView(model);
    }

    public async Task<IReadOnlyList<ModelView>> ListModelsAsync(CancellationToken ct)
    {
        var models = await _models.ListAsync(new AllModelPortfoliosSpecification(), ct);
        return models.Select(ToView).ToList();
    }

    public async Task ChooseModelAsync(string shopperUserName, string modelKey, CancellationToken ct)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByUserNameSpecification(shopperUserName), ct);
        if (investor is null)
        {
            throw new PortfolioStateException("You are not signed up as an investor.");
        }

        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByKeySpecification(modelKey), ct);
        if (model is null)
        {
            throw new PortfolioValidationException($"Unknown model '{modelKey}'.");
        }
        if (model.UpvestAllocationId is null)
        {
            throw new PortfolioStateException($"Model '{modelKey}' is not yet available at the investment provider.");
        }
        if (investor.UpvestAccountId is null)
        {
            throw new PortfolioStateException("Your investor account is not ready yet.");
        }

        await _gateway.SetFollowedModelAsync(investor.UpvestAccountId.Value, model.UpvestAllocationId.Value, investor.ModelChoiceIdempotencyKey, ct);
        investor.ChooseModel(model.Id);
        await _investors.UpdateAsync(investor, ct);
        _logger.LogInformation("Investor chose model {ModelKey} (allocation {AllocationId}).", modelKey, model.UpvestAllocationId);
    }

    private static void ValidateFunds(IReadOnlyList<FundWeight> funds)
    {
        if (funds is null || funds.Count == 0)
        {
            throw new PortfolioValidationException("A model must have at least one fund.");
        }
        if (funds.Any(f => string.IsNullOrWhiteSpace(f.InstrumentId)))
        {
            throw new PortfolioValidationException("Every fund must have an instrumentId (ISIN).");
        }
        if (funds.Any(f => f.Weight <= 0))
        {
            throw new PortfolioValidationException("Every fund weight must be greater than zero.");
        }
        if (funds.Select(f => f.InstrumentId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != funds.Count)
        {
            throw new PortfolioValidationException("A fund (ISIN) may appear only once in a model.");
        }
        var total = funds.Sum(f => f.Weight);
        if (total != 100m)
        {
            throw new PortfolioValidationException($"Fund weights must add up to exactly 100 (got {total}).");
        }
    }

    private async Task ValidateIsinsAsync(IReadOnlyList<FundWeight> funds, CancellationToken ct)
    {
        IReadOnlySet<string> tradeable;
        try
        {
            tradeable = await _gateway.ListTradeableIsinsAsync(ct);
        }
        catch (UpvestGatewayException ex)
        {
            // Can't reach the instrument list (e.g. the sandbox scope wall). Skip client-side validation;
            // the allocation create below still enforces the ISINs at Upvest.
            _logger.LogWarning("Could not list instruments to validate ISINs (status {Status}); skipping pre-check.", ex.StatusCode);
            return;
        }

        var unknown = funds.Select(f => f.InstrumentId)
            .Where(isin => !tradeable.Contains(isin))
            .ToList();
        if (unknown.Count > 0)
        {
            throw new PortfolioValidationException($"Unknown or non-tradeable fund ISIN(s): {string.Join(", ", unknown)}.");
        }
    }

    private static ModelView ToView(ModelPortfolio model) =>
        new(model.ModelKey, model.Name,
            model.Funds.Select(f => new FundWeight(f.InstrumentId, f.Weight)).ToList());
}
