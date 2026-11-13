using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-defined model portfolio: a named mix of funds, each with a target weight.
/// Mapped to a Upvest portfolio allocation (<see cref="UpvestAllocationId"/>) that investors follow.
/// </summary>
public class ModelPortfolio : BaseEntity, IAggregateRoot
{
    /// <summary>The operator-chosen identifier of the model (the <c>{modelId}</c> route value). Unique.</summary>
    public string ModelId { get; private set; }

    public string Name { get; private set; }

    /// <summary>The Upvest portfolio allocation this model maps to. Null until the first successful create at Upvest.</summary>
    public Guid? UpvestAllocationId { get; private set; }

    /// <summary>Idempotency key for the allocation create at Upvest (persisted for safe replay).</summary>
    public Guid AllocationIdempotencyKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<ModelFund> _funds = new();
    public IReadOnlyCollection<ModelFund> Funds => _funds.AsReadOnly();

#pragma warning disable CS8618 // Required by Entity Framework
    private ModelPortfolio() { }
#pragma warning restore CS8618

    public ModelPortfolio(string modelId, string name)
    {
        ModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        Name = Guard.Against.NullOrEmpty(name, nameof(name));
        AllocationIdempotencyKey = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    /// <summary>Replace the model's name and funds. Enforces that fund weights sum to exactly 100.</summary>
    public void Replace(string name, IEnumerable<ModelFund> funds)
    {
        Name = Guard.Against.NullOrEmpty(name, nameof(name));
        var list = Guard.Against.Null(funds, nameof(funds)).ToList();
        Guard.Against.NullOrEmpty(list, nameof(funds));

        var total = list.Sum(f => f.Weight);
        if (total != 100m)
        {
            throw new ArgumentException($"Fund weights must sum to exactly 100, but summed to {total}.", nameof(funds));
        }

        _funds.Clear();
        _funds.AddRange(list);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetUpvestAllocation(Guid allocationId)
    {
        UpvestAllocationId = allocationId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
