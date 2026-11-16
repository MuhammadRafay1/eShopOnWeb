using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-defined model portfolio: a named mix of funds with a target weight each.
/// The shop's <see cref="ModelId"/> maps to Upvest's allocation id (<see cref="UpvestAllocationId"/>).
/// </summary>
public class ModelPortfolio : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private ModelPortfolio() { }
#pragma warning restore CS8618

    public ModelPortfolio(string modelId, string name, IEnumerable<ModelFund> funds)
    {
        ModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        Replace(name, funds);
    }

    /// <summary>The operator-chosen model id (from the PUT route). Unique.</summary>
    public string ModelId { get; private set; }

    public string Name { get; private set; }

    /// <summary>The Upvest allocation id this model maps to, once created at Upvest.</summary>
    public Guid? UpvestAllocationId { get; private set; }

    private readonly List<ModelFund> _funds = new();
    public IReadOnlyCollection<ModelFund> Funds => _funds.AsReadOnly();

    /// <summary>Create or fully replace this model's name and funds. Weights must total exactly 100.</summary>
    public void Replace(string name, IEnumerable<ModelFund> funds)
    {
        Name = Guard.Against.NullOrEmpty(name, nameof(name));
        var list = Guard.Against.Null(funds, nameof(funds)).ToList();
        Guard.Against.NullOrEmpty(list, nameof(funds));

        var total = list.Sum(f => f.Weight);
        if (total != 100m)
        {
            throw new ArgumentException($"The weights of a model portfolio must total exactly 100, but totalled {total}.", nameof(funds));
        }

        _funds.Clear();
        _funds.AddRange(list);
    }

    public void SetAllocationId(Guid allocationId) => UpvestAllocationId = allocationId;
}
