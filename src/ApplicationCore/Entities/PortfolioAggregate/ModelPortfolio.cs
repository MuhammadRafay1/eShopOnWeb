using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A model portfolio defined by the shop's operator: a named mix of funds with a target weight
/// per fund (weights sum to exactly 100). Mirrored to Upvest as a portfolio allocation; the
/// latest allocation id is kept so investors can be configured to follow it and realignments can
/// bring them back to its weights.
/// </summary>
public class ModelPortfolio : BaseEntity, IAggregateRoot
{
    private readonly List<ModelPortfolioFund> _funds = new();

#pragma warning disable CS8618 // Required by Entity Framework
    private ModelPortfolio() { }
#pragma warning restore CS8618

    public ModelPortfolio(string modelId, string name, IEnumerable<ModelPortfolioFund> funds)
    {
        ModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        Name = Guard.Against.NullOrEmpty(name, nameof(name));
        CreatedAt = DateTimeOffset.UtcNow;
        ReplaceFunds(name, funds);
    }

    /// <summary>The operator-chosen identifier for this model (from the route).</summary>
    public string ModelId { get; private set; }

    public string Name { get; private set; }

    /// <summary>The id of the latest Upvest allocation that mirrors this model, if provisioned.</summary>
    public string? UpvestAllocationId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<ModelPortfolioFund> Funds => _funds.AsReadOnly();

    /// <summary>Replaces the model's name and funds, enforcing that weights sum to exactly 100.</summary>
    public void ReplaceFunds(string name, IEnumerable<ModelPortfolioFund> funds)
    {
        Name = Guard.Against.NullOrEmpty(name, nameof(name));
        var list = funds?.ToList() ?? new List<ModelPortfolioFund>();
        Guard.Against.NullOrEmpty(list, nameof(funds));

        var total = list.Sum(f => f.Weight);
        if (total != 100m)
        {
            throw new ArgumentException(
                $"The weights of a model must add up to exactly 100 (got {total}).", nameof(funds));
        }

        _funds.Clear();
        _funds.AddRange(list);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetAllocationId(string allocationId)
    {
        UpvestAllocationId = Guard.Against.NullOrEmpty(allocationId, nameof(allocationId));
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
