using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.Extensions.Logging;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Models;
using UpvestInvestmentApi.Models.AnyOf;
using ReqPortfolios = UpvestInvestmentApi.Requests.Portfolios;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio.Services;

/// <summary>
/// Flow 2 — operator-defined model portfolios (Upvest allocations) and the investor choosing one (a Upvest
/// configuration linking their account to an allocation). The operator's model set is the app's own source of
/// truth; the matching Upvest allocation is synced best-effort on write and ensured on demand when a later flow
/// (choose/pay-in/realign) actually needs it.
/// </summary>
public sealed class ModelPortfolioService : IModelPortfolioService
{
    private static readonly Regex IsinPattern = new("^[A-Z]{2}[A-Z0-9]{9}[0-9]$", RegexOptions.Compiled);

    private readonly UpvestInvestmentApiClient _client;
    private readonly PortfolioContext _db;
    private readonly ILogger<ModelPortfolioService> _logger;

    public ModelPortfolioService(UpvestInvestmentApiClient client, PortfolioContext db, ILogger<ModelPortfolioService> logger)
    {
        _client = client;
        _db = db;
        _logger = logger;
    }

    public async Task<ModelResult> UpsertModelAsync(string modelId, string name, IReadOnlyList<FundWeight> funds, CancellationToken ct)
    {
        Validate(name, funds);

        var model = await _db.Models.Include(m => m.Funds).FirstOrDefaultAsync(m => m.ModelId == modelId, ct);
        if (model is null)
        {
            model = new ModelPortfolio { ModelId = modelId, AllocationIdempotencyKey = Guid.NewGuid() };
            _db.Models.Add(model);
        }
        else
        {
            _db.ModelFunds.RemoveRange(model.Funds);
            model.Funds.Clear();
        }

        model.Name = name;
        model.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var f in funds)
            model.Funds.Add(new ModelFund { ModelId = modelId, InstrumentId = f.InstrumentId.Trim().ToUpperInvariant(), Weight = f.Weight });

        await _db.SaveChangesAsync(ct);

        // Best-effort: push the definition to Upvest now; never let a provider outage lose the operator's model.
        try
        {
            await SyncAllocationAsync(model, ct);
            await _db.SaveChangesAsync(ct);
        }
        catch (PortfolioException ex)
        {
            _logger.LogWarning(ex, "Model {ModelId} saved; its Upvest allocation will be synced on next use.", modelId);
        }

        return ToResult(model);
    }

    public async Task<IReadOnlyList<ModelResult>> ListModelsAsync(CancellationToken ct)
    {
        var models = await _db.Models.Include(m => m.Funds).AsNoTracking().ToListAsync(ct);
        return models.Select(ToResult).ToList();
    }

    public async Task ChooseModelAsync(string shopperId, string modelId, CancellationToken ct)
    {
        var investor = await _db.Investors.FirstOrDefaultAsync(i => i.ShopperId == shopperId, ct);
        if (investor?.AccountId is null)
            throw new PortfolioConflictException("You must complete investor sign-up before choosing a model.");

        var model = await _db.Models.Include(m => m.Funds).FirstOrDefaultAsync(m => m.ModelId == modelId, ct);
        if (model is null)
            throw new PortfolioNotFoundException($"Model '{modelId}' does not exist.");

        var allocationId = await EnsureAllocationAsync(model, ct);
        await _db.SaveChangesAsync(ct);

        var accountId = investor.AccountId.Value;
        var switching = investor.ChosenModelId is not null;

        if (switching)
        {
            await UpvestCall.WriteAsync(() => _client.Portfolios.UpdatePortfoliosConfiguration(
                new ReqPortfolios.UpdatePortfoliosConfigurationRequest
                {
                    AccountId = accountId,
                    IdempotencyKey = Guid.NewGuid(),
                    Body = new PortfoliosConfigurationUpdateRequest { AllocationId = allocationId }
                }, cancellationToken: ct), "UpdatePortfoliosConfiguration", _logger);
        }
        else
        {
            await UpvestCall.WriteAsync(() => _client.Portfolios.CreatePortfoliosConfiguration(
                new ReqPortfolios.CreatePortfoliosConfigurationRequest
                {
                    IdempotencyKey = Guid.NewGuid(),
                    Body = new PortfoliosConfigurationCreateRequest { AccountId = accountId, AllocationId = allocationId }
                }, cancellationToken: ct), "CreatePortfoliosConfiguration", _logger);
        }

        investor.ChosenModelId = modelId;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Ensures the model has a backing Upvest allocation, creating it if necessary. Provider errors propagate.</summary>
    private async Task<Guid> EnsureAllocationAsync(ModelPortfolio model, CancellationToken ct)
    {
        if (model.AllocationId is not null) return model.AllocationId.Value;
        await SyncAllocationAsync(model, ct);
        return model.AllocationId!.Value;
    }

    /// <summary>Creates (first time) or replaces (full set) the Upvest allocation for this model.</summary>
    private async Task SyncAllocationAsync(ModelPortfolio model, CancellationToken ct)
    {
        var allocations = BuildAllocations(model.Funds);

        if (model.AllocationId is null)
        {
            var created = await UpvestCall.WriteAsync(() => _client.Portfolios.CreatePortfoliosAllocation(
                new ReqPortfolios.CreatePortfoliosAllocationRequest
                {
                    IdempotencyKey = model.AllocationIdempotencyKey,
                    Body = new PortfoliosAllocationCreateRequest { Name = model.Name, Allocation = allocations }
                }, cancellationToken: ct), "CreatePortfoliosAllocation", _logger);
            model.AllocationId = created.Id;
        }
        else
        {
            await UpvestCall.WriteAsync(() => _client.Portfolios.UpdatePortfoliosAllocation(
                new ReqPortfolios.UpdatePortfoliosAllocationRequest
                {
                    AllocationId = model.AllocationId.Value,
                    Body = new PortfoliosAllocationUpdateRequest { Name = model.Name, Allocation = allocations }
                }, cancellationToken: ct), "UpdatePortfoliosAllocation", _logger);
        }
    }

    private static IReadOnlyList<Allocation> BuildAllocations(IEnumerable<ModelFund> funds) =>
        funds.Select(f => new Allocation
        {
            InstrumentId = InstrumentId.String(f.InstrumentId),
            InstrumentIdType = UpvestInvestmentApi.Models.Enums.InstrumentIdType4.Isin,
            Weight = UpvestMapping.Weight(f.Weight)
        }).ToList();

    private static void Validate(string name, IReadOnlyList<FundWeight> funds)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new PortfolioValidationException("A model name is required.");
        if (funds is null || funds.Count == 0)
            throw new PortfolioValidationException("A model must have at least one fund.");

        foreach (var f in funds)
        {
            if (string.IsNullOrWhiteSpace(f.InstrumentId) || !IsinPattern.IsMatch(f.InstrumentId.Trim().ToUpperInvariant()))
                throw new PortfolioValidationException($"'{f.InstrumentId}' is not a valid ISIN.");
            if (f.Weight <= 0m || f.Weight > 100m)
                throw new PortfolioValidationException("Each fund weight must be greater than 0 and at most 100.");
        }

        if (funds.Sum(f => f.Weight) != 100m)
            throw new PortfolioValidationException("The fund weights of a model must add up to exactly 100.");
    }

    private static ModelResult ToResult(ModelPortfolio m) =>
        new(m.ModelId, m.Name, m.Funds.Select(f => new FundWeight(f.InstrumentId, f.Weight)).ToList());
}
