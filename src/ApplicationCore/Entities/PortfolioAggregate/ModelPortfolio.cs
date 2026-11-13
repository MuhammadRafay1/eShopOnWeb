using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A model portfolio defined by the shop operator: a named mix of funds with a target
/// weight per fund. Mirrored to Upvest as a portfolio allocation; the <see cref="UpvestAllocationId"/>
/// is captured once the allocation has been created there.
/// </summary>
public class ModelPortfolio : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private ModelPortfolio() { }
#pragma warning restore CS8618

    public ModelPortfolio(string modelId, string name, IEnumerable<ModelFund> funds)
    {
        Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        ModelId = modelId;
        CreatedAt = DateTimeOffset.UtcNow;
        Replace(name, funds);
    }

    /// <summary>The operator-chosen identifier used in the route (<c>{modelId}</c>).</summary>
    public string ModelId { get; private set; }

    public string Name { get; private set; }

    /// <summary>The Upvest allocation id this model is mirrored to, once synced.</summary>
    public string? UpvestAllocationId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<ModelFund> _funds = new();
    public IReadOnlyCollection<ModelFund> Funds => _funds.AsReadOnly();

    /// <summary>The weight percentages of a model must add up to exactly 100.</summary>
    public const decimal RequiredWeightTotal = 100m;

    public void Replace(string name, IEnumerable<ModelFund> funds)
    {
        Guard.Against.NullOrEmpty(name, nameof(name));
        var fundList = funds?.ToList() ?? new List<ModelFund>();
        Guard.Against.NullOrEmpty(fundList, nameof(funds));

        var total = fundList.Sum(f => f.Weight);
        if (total != RequiredWeightTotal)
        {
            throw new ArgumentException(
                $"The weights of a model must add up to exactly {RequiredWeightTotal}, but they add up to {total}.",
                nameof(funds));
        }

        var duplicateIsin = fundList.GroupBy(f => f.InstrumentId)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateIsin is not null)
        {
            throw new ArgumentException(
                $"A fund (ISIN {duplicateIsin.Key}) appears more than once in the model.", nameof(funds));
        }

        Name = name;
        _funds.Clear();
        _funds.AddRange(fundList);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetAllocationId(string allocationId)
    {
        Guard.Against.NullOrEmpty(allocationId, nameof(allocationId));
        UpvestAllocationId = allocationId;
    }
}
