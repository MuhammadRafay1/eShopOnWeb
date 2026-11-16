using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-defined model portfolio: a named mix of funds with a target weight per fund.
/// <para>
/// Keyed by the app-owned <see cref="ModelId"/> (the route id), which maps to Upvest's server-generated
/// <see cref="AllocationId"/>. This lets <c>PUT /api/portfolio/models/{modelId}</c> be create-or-replace and lets
/// the realignment route resolve a model id to its Upvest allocation.
/// </para>
/// </summary>
public class ModelPortfolio : IAggregateRoot
{
    private readonly List<ModelPortfolioFund> _funds = new();

    private ModelPortfolio()
    {
        // Required by EF Core.
        ModelId = string.Empty;
        Name = string.Empty;
    }

    public ModelPortfolio(string modelId, string name)
    {
        ModelId = !string.IsNullOrWhiteSpace(modelId)
            ? modelId
            : throw new ArgumentException("Model id is required.", nameof(modelId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        AllocationIdempotencyKey = Guid.NewGuid();
    }

    /// <summary>App-owned primary key, supplied as the route id.</summary>
    public string ModelId { get; private set; }

    public string Name { get; private set; }

    /// <summary>The Upvest allocation id. Null until the allocation has been created at Upvest.</summary>
    public Guid? AllocationId { get; private set; }

    /// <summary>Idempotency key for the create-allocation write, persisted so a replay reuses it.</summary>
    public Guid AllocationIdempotencyKey { get; private set; }

    public IReadOnlyCollection<ModelPortfolioFund> Funds => _funds.AsReadOnly();

    public void SetAllocationId(Guid allocationId) => AllocationId = allocationId;

    public void Replace(string name, IEnumerable<ModelPortfolioFund> funds)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _funds.Clear();
        _funds.AddRange(funds);
    }
}

/// <summary>A single fund in a model portfolio: an instrument (ISIN) and its target weight percentage.</summary>
public class ModelPortfolioFund
{
    private ModelPortfolioFund()
    {
        // Required by EF Core.
        InstrumentId = string.Empty;
    }

    public ModelPortfolioFund(string instrumentId, decimal weight)
    {
        InstrumentId = !string.IsNullOrWhiteSpace(instrumentId)
            ? instrumentId
            : throw new ArgumentException("Instrument id (ISIN) is required.", nameof(instrumentId));
        Weight = weight;
    }

    public int Id { get; private set; }

    /// <summary>The fund's ISIN.</summary>
    public string InstrumentId { get; private set; }

    /// <summary>The target weight, as a percentage.</summary>
    public decimal Weight { get; private set; }
}
