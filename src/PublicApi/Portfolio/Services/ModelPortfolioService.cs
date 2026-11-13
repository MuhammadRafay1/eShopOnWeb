using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.PublicApi.Portfolio.Data;
using Microsoft.eShopWeb.PublicApi.Portfolio.Domain;
using Microsoft.eShopWeb.PublicApi.Portfolio.Upvest;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Services;

/// <summary>One fund of a model as supplied by the operator (ISIN + percentage weight).</summary>
public sealed record ModelFundInput(string InstrumentId, decimal Weight);

/// <summary>A model and its funds, as returned to callers.</summary>
public sealed record ModelPortfolioResult(string ModelId, string Name, IReadOnlyList<ModelFundInput> Funds);

/// <summary>
/// Flow 2 — operator model portfolios. The operator owns the set of models; each is mirrored to an Upvest allocation.
/// Weights must sum to exactly 100 and each fund must be a tradable ISIN.
/// </summary>
public sealed class ModelPortfolioService
{
    private static readonly Regex IsinPattern = new("^[A-Z]{2}[A-Z0-9]{9}[0-9]$", RegexOptions.Compiled);

    private readonly PortfolioContext _db;
    private readonly IUpvestGateway _gateway;
    private readonly ILogger<ModelPortfolioService> _logger;

    public ModelPortfolioService(PortfolioContext db, IUpvestGateway gateway, ILogger<ModelPortfolioService> logger)
    {
        _db = db;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<ModelPortfolioResult> PutModelAsync(string modelId, string name, IReadOnlyList<ModelFundInput> funds, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new PortfolioRequestException("A model name is required.", 400);
        if (funds is null || funds.Count == 0)
            throw new PortfolioRequestException("A model must have at least one fund.", 400);

        foreach (var f in funds)
        {
            if (string.IsNullOrWhiteSpace(f.InstrumentId) || !IsinPattern.IsMatch(f.InstrumentId))
                throw new PortfolioRequestException($"'{f.InstrumentId}' is not a valid ISIN.", 400);
            if (f.Weight < 0m || f.Weight > 100m)
                throw new PortfolioRequestException("Each weight must be between 0 and 100.", 400);
        }
        if (funds.Select(f => f.InstrumentId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != funds.Count)
            throw new PortfolioRequestException("A fund may appear only once in a model.", 400);
        if (funds.Sum(f => f.Weight) != 100m)
            throw new PortfolioRequestException("The fund weights must add up to exactly 100.", 400);

        await ValidateIsinsAsync(funds, ct);

        var fundWeights = funds.Select(f => new FundWeight(f.InstrumentId, f.Weight)).ToList();

        var model = await _db.Models.Include(m => m.Funds).FirstOrDefaultAsync(m => m.ModelId == modelId, ct);
        var isNew = model is null;
        if (model is null)
        {
            model = new ModelPortfolio { ModelId = modelId };
            _db.Models.Add(model);
        }

        model.Name = name;
        model.Funds.Clear();
        model.Funds.AddRange(funds.Select(f => new ModelFund { ModelId = modelId, InstrumentId = f.InstrumentId, Weight = f.Weight }));
        model.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Mirror to Upvest: create the allocation on first write, replace it on later writes. In the sandbox this needs
        // scopes the client lacks; the local model (the operator's offering) stands regardless.
        try
        {
            if (model.AllocationId is { } allocationId)
            {
                await _gateway.UpdateAllocationAsync(allocationId, name, fundWeights, ct);
            }
            else
            {
                var created = await _gateway.CreateAllocationAsync(name, fundWeights, model.AllocationIdempotencyKey, ct);
                model.AllocationId = created.AllocationId;
                await _db.SaveChangesAsync(ct);
            }
        }
        catch (UpvestProviderException ex)
        {
            _logger.LogWarning("Model {ModelId}: allocation not mirrored to Upvest ({Status}); stored locally.",
                modelId, (int?)ex.StatusCode);
        }

        return ToResult(model);
    }

    public async Task<IReadOnlyList<ModelPortfolioResult>> ListModelsAsync(CancellationToken ct)
    {
        var models = await _db.Models.Include(m => m.Funds).AsNoTracking().ToListAsync(ct);
        return models.Select(ToResult).ToList();
    }

    private async Task ValidateIsinsAsync(IReadOnlyList<ModelFundInput> funds, CancellationToken ct)
    {
        try
        {
            var tradable = await _gateway.ListTradableIsinsAsync(ct);
            if (tradable.Count == 0)
                return; // nothing to validate against
            var unknown = funds.Where(f => !tradable.Contains(f.InstrumentId)).Select(f => f.InstrumentId).ToList();
            if (unknown.Count > 0)
                throw new PortfolioRequestException($"Unknown instrument(s): {string.Join(", ", unknown)}.", 400);
        }
        catch (UpvestProviderException ex)
        {
            // The sandbox client cannot list instruments; fall back to the ISIN-shape check already done above.
            _logger.LogWarning("Instrument validation skipped ({Status}); relied on ISIN format only.", (int?)ex.StatusCode);
        }
    }

    private static ModelPortfolioResult ToResult(ModelPortfolio m) =>
        new(m.ModelId, m.Name, m.Funds
            .OrderBy(f => f.Id)
            .Select(f => new ModelFundInput(f.InstrumentId, f.Weight)).ToList());
}
