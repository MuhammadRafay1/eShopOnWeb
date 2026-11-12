using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A model portfolio the operator defines: a mix of funds with a target weight per fund.
/// Keyed by the operator-supplied <see cref="ModelId"/> (the path parameter of the PUT), which the app
/// maps to the Upvest allocation id that backs it.
/// </summary>
public class ModelPortfolio : IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private ModelPortfolio() { }
#pragma warning restore CS8618

    public ModelPortfolio(string modelId, string name, IEnumerable<ModelFund> funds)
    {
        ModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        Replace(name, funds);
    }

    /// <summary>Operator-supplied identifier (primary key).</summary>
    public string ModelId { get; private set; }

    public string Name { get; private set; }

    /// <summary>The Upvest allocation id backing this model, once it has been created/replaced at Upvest.</summary>
    public Guid? UpvestAllocationId { get; private set; }

    private readonly List<ModelFund> _funds = new();
    public IReadOnlyCollection<ModelFund> Funds => _funds.AsReadOnly();

    /// <summary>Replace the model's name and funds (create-or-replace PUT semantics). Weights must sum to exactly 100.</summary>
    public void Replace(string name, IEnumerable<ModelFund> funds)
    {
        Name = Guard.Against.NullOrEmpty(name, nameof(name));
        var list = funds?.ToList() ?? throw new ArgumentNullException(nameof(funds));
        Guard.Against.NullOrEmpty(list, nameof(funds));

        var total = list.Sum(f => f.Weight);
        if (total != 100m)
        {
            throw new ArgumentException($"Fund weights must sum to exactly 100, but summed to {total}.", nameof(funds));
        }

        _funds.Clear();
        _funds.AddRange(list);
    }

    public void LinkAllocation(Guid allocationId) => UpvestAllocationId = allocationId;
}
