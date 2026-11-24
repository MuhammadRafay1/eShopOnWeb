using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-defined model portfolio: a named mix of funds with a target weight per fund
/// (weights sum to exactly 100). Mapped to an Upvest allocation.
/// </summary>
public class ModelPortfolio : BaseEntity, IAggregateRoot
{
    public string ModelId { get; private set; }
    public string Name { get; private set; }
    public string? UpvestAllocationId { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<ModelFund> _funds = new();
    public IReadOnlyCollection<ModelFund> Funds => _funds.AsReadOnly();

    private ModelPortfolio()
    {
        ModelId = string.Empty;
        Name = string.Empty;
    }

    public ModelPortfolio(string modelId, string name, IEnumerable<ModelFund> funds)
    {
        ModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        Name = Guard.Against.NullOrEmpty(name, nameof(name));
        Replace(name, funds);
    }

    public void Replace(string name, IEnumerable<ModelFund> funds)
    {
        Name = Guard.Against.NullOrEmpty(name, nameof(name));
        var materialised = funds?.ToList() ?? new List<ModelFund>();
        Guard.Against.NullOrEmpty(materialised, nameof(funds));

        var totalWeight = materialised.Sum(f => f.Weight);
        if (totalWeight != 100m)
            throw new ArgumentException($"Fund weights must sum to exactly 100 but summed to {totalWeight}.", nameof(funds));

        _funds.Clear();
        _funds.AddRange(materialised);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetAllocationId(string allocationId)
    {
        UpvestAllocationId = Guard.Against.NullOrEmpty(allocationId, nameof(allocationId));
    }
}
