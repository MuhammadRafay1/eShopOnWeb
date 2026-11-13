using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A model portfolio the operator defines: a named mix of funds with a target weight per fund.
/// Keyed by the operator-supplied <see cref="ModelId"/>. Maps to a Upvest portfolio allocation,
/// whose id is stored in <see cref="UpvestAllocationId"/> once created.
/// </summary>
public class ModelPortfolio : IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private ModelPortfolio() { }
#pragma warning restore CS8618

    public ModelPortfolio(string modelId, string name, IEnumerable<ModelFund> funds)
    {
        Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        ModelId = modelId;
        IdempotencyKey = Guid.NewGuid();
        Replace(name, funds);
    }

    /// <summary>The operator-supplied identifier (route <c>{modelId}</c>). Primary key.</summary>
    public string ModelId { get; private set; }

    public string Name { get; private set; }

    /// <summary>The Upvest portfolio-allocation id this model maps to, once created at Upvest.</summary>
    public Guid? UpvestAllocationId { get; private set; }

    /// <summary>Idempotency key for the allocation create, persisted for replay.</summary>
    public Guid IdempotencyKey { get; private set; }

    private readonly List<ModelFund> _funds = new();
    public IReadOnlyCollection<ModelFund> Funds => _funds.AsReadOnly();

    public void SetAllocationId(Guid allocationId) => UpvestAllocationId = allocationId;

    /// <summary>Create-or-replace the model's name and funds. Weights must add up to exactly 100.</summary>
    public void Replace(string name, IEnumerable<ModelFund> funds)
    {
        Guard.Against.NullOrEmpty(name, nameof(name));
        Guard.Against.Null(funds, nameof(funds));

        var list = funds.ToList();
        Guard.Against.NullOrEmpty(list, nameof(funds));

        var total = list.Sum(f => f.Weight);
        if (total != 100m)
        {
            throw new ArgumentException($"Fund weights must add up to exactly 100, but they add up to {total}.", nameof(funds));
        }

        if (list.Select(f => f.InstrumentId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != list.Count)
        {
            throw new ArgumentException("A model portfolio cannot list the same fund (ISIN) more than once.", nameof(funds));
        }

        Name = name;
        _funds.Clear();
        _funds.AddRange(list);
    }
}
