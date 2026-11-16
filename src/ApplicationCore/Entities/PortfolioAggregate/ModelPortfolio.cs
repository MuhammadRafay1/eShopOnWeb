using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-defined model portfolio: a named mix of funds with a target weight per fund.
/// Maps the shop's own <c>modelId</c> (<see cref="ModelKey"/>) onto an Upvest allocation.
/// </summary>
public class ModelPortfolio : BaseEntity, IAggregateRoot
{
    /// <summary>The shop's own model identifier, taken from the route (<c>{modelId}</c>).</summary>
    public string ModelKey { get; private set; } = default!;

    public string Name { get; private set; } = default!;

    /// <summary>The Upvest allocation this model is realized as (null until created at Upvest).</summary>
    public Guid? UpvestAllocationId { get; private set; }

    /// <summary>Persisted idempotency key for the allocation create call (reused on retry).</summary>
    public Guid AllocationIdempotencyKey { get; private set; }

    private readonly List<ModelFund> _funds = new();
    public IReadOnlyCollection<ModelFund> Funds => _funds.AsReadOnly();

    private ModelPortfolio() { } // EF

    public ModelPortfolio(string modelKey, string name, IEnumerable<ModelFund> funds)
    {
        ModelKey = modelKey;
        AllocationIdempotencyKey = Guid.NewGuid();
        Replace(name, funds);
    }

    /// <summary>Create-or-replace the model's name and funds.</summary>
    public void Replace(string name, IEnumerable<ModelFund> funds)
    {
        Name = name;
        _funds.Clear();
        _funds.AddRange(funds);
    }

    public void SetAllocationId(Guid allocationId) => UpvestAllocationId = allocationId;
}

/// <summary>A single fund in a model portfolio: the fund's ISIN and its target weight (a percentage).</summary>
public class ModelFund : BaseEntity
{
    public string InstrumentId { get; private set; } = default!;
    public decimal Weight { get; private set; }

    private ModelFund() { } // EF

    public ModelFund(string instrumentId, decimal weight)
    {
        InstrumentId = instrumentId;
        Weight = weight;
    }
}
