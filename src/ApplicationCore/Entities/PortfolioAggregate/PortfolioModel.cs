using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-defined model portfolio: a named mix of funds with a target weight each. Keyed by the
/// operator's own <see cref="ModelId"/> (the route id), which is the primary key, so a re-PUT of the same id
/// is an idempotent create-or-replace. Maps the operator's model id to the Upvest allocation id it is backed
/// by.
/// </summary>
public class PortfolioModel : IAggregateRoot
{
    private readonly List<PortfolioModelFund> _funds = new();

#pragma warning disable CS8618 // Required by Entity Framework
    private PortfolioModel() { }
#pragma warning restore CS8618

    public PortfolioModel(string modelId, string name)
    {
        ModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        Name = Guard.Against.NullOrEmpty(name, nameof(name));
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    /// <summary>The operator's model identifier (the <c>{modelId}</c> route value). Primary key.</summary>
    public string ModelId { get; private set; }

    public string Name { get; private set; }

    /// <summary>The Upvest allocation id this model is backed by, once created at Upvest.</summary>
    public Guid? UpvestAllocationId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<PortfolioModelFund> Funds => _funds.AsReadOnly();

    public void Rename(string name)
    {
        Name = Guard.Against.NullOrEmpty(name, nameof(name));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ReplaceFunds(IEnumerable<PortfolioModelFund> funds)
    {
        Guard.Against.Null(funds, nameof(funds));
        _funds.Clear();
        _funds.AddRange(funds);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetAllocationId(Guid allocationId)
    {
        UpvestAllocationId = allocationId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>The sum of the fund weights; must equal exactly 100 for a valid model.</summary>
    public decimal TotalWeight() => _funds.Sum(f => f.Weight);
}
