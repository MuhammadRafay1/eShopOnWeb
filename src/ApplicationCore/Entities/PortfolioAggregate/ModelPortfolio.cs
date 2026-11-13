using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-defined model portfolio: a named mix of funds with a target weight per fund,
/// whose weights sum to exactly 100. Maps the caller-facing <see cref="ModelId"/> onto the
/// Upvest portfolio allocation it is stored as.
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
        AllocationIdempotencyKey = Guid.NewGuid();
        Replace(name, funds);
    }

    /// <summary>The caller-supplied model id (route value). Unique.</summary>
    public string ModelId { get; private set; }

    /// <summary>The Upvest portfolio allocation id this model is stored as. Null until first synced.</summary>
    public Guid? UpvestAllocationId { get; private set; }

    public string Name { get; private set; }

    private readonly List<ModelFund> _funds = new();
    public IReadOnlyCollection<ModelFund> Funds => _funds.AsReadOnly();

    public Guid AllocationIdempotencyKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Create-or-replace the model's name and funds. Validates the weight sum.</summary>
    public void Replace(string name, IEnumerable<ModelFund> funds)
    {
        Guard.Against.NullOrEmpty(name, nameof(name));
        Guard.Against.Null(funds, nameof(funds));

        var list = funds.ToList();
        Guard.Against.NullOrEmpty(list, nameof(funds));

        var total = list.Sum(f => f.Weight);
        if (total != 100m)
        {
            throw new ArgumentException($"Model portfolio fund weights must sum to exactly 100, but summed to {total}.", nameof(funds));
        }

        Name = name;
        _funds.Clear();
        _funds.AddRange(list);
    }

    public void SetUpvestAllocation(Guid allocationId) => UpvestAllocationId = allocationId;
}
