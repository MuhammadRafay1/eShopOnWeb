using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A model portfolio the shop operator defines: a named mix of funds with a target weight per fund.
/// Keyed by the shop's own <see cref="ModelId"/> (supplied in the PUT route), mapped to the Upvest
/// portfolio allocation that realises it. A PUT replaces the model in place (same id), so the mapping
/// to <see cref="UpvestAllocationId"/> stays stable across edits.
/// </summary>
public class ModelPortfolio : IAggregateRoot
{
    /// <summary>Shop-defined model id. Primary key.</summary>
    public string ModelId { get; private set; }

    public string Name { get; private set; }

    /// <summary>The Upvest portfolio allocation id that backs this model, once created.</summary>
    public Guid? UpvestAllocationId { get; private set; }

    private readonly List<ModelFund> _funds = new();
    public IReadOnlyCollection<ModelFund> Funds => _funds.AsReadOnly();

    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

#pragma warning disable CS8618 // Required by Entity Framework
    private ModelPortfolio() { }
#pragma warning restore CS8618

    public ModelPortfolio(string modelId, string name)
    {
        ModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        Name = Guard.Against.NullOrEmpty(name, nameof(name));
    }

    /// <summary>Replace the model's name and the complete set of funds (weights must total 100 — enforced by the caller).</summary>
    public void Replace(string name, IEnumerable<ModelFund> funds)
    {
        Name = Guard.Against.NullOrEmpty(name, nameof(name));
        _funds.Clear();
        _funds.AddRange(funds);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetAllocation(Guid allocationId) => UpvestAllocationId = allocationId;

    public decimal TotalWeight => _funds.Sum(f => f.Weight);
}
