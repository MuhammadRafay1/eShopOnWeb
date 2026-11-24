using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-defined model portfolio: a named mix of funds with a target weight per fund.
/// Mirrors a portfolio "allocation" at Upvest; <see cref="UpvestAllocationId"/> is the link.
/// </summary>
public class ModelPortfolio : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private ModelPortfolio() { }
#pragma warning restore CS8618

    public ModelPortfolio(string modelId, string name, IEnumerable<ModelFund> funds)
    {
        ModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        Update(name, funds);
    }

    /// <summary>Operator-supplied external identifier (the {modelId} in the route).</summary>
    public string ModelId { get; private set; }

    public string Name { get; private set; }

    public Guid? UpvestAllocationId { get; private set; }

    private readonly List<ModelFund> _funds = new();
    public IReadOnlyCollection<ModelFund> Funds => _funds.AsReadOnly();

    /// <summary>Replaces the model's name and funds. Weights must sum to exactly 100.</summary>
    public void Update(string name, IEnumerable<ModelFund> funds)
    {
        Name = Guard.Against.NullOrEmpty(name, nameof(name));
        var fundList = Guard.Against.Null(funds, nameof(funds)).ToList();
        Guard.Against.NullOrEmpty(fundList, nameof(funds));

        var total = fundList.Sum(f => f.Weight);
        if (total != 100m)
        {
            throw new ArgumentException($"Fund weights must sum to exactly 100, but summed to {total}.", nameof(funds));
        }

        _funds.Clear();
        _funds.AddRange(fundList);
    }

    public void LinkAllocation(Guid allocationId) =>
        UpvestAllocationId = Guard.Against.Default(allocationId, nameof(allocationId));
}
