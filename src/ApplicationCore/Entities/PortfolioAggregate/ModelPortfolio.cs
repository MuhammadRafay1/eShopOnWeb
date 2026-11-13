using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A model portfolio defined by the operator: a named mix of funds with a target weight per fund.
/// The weights add up to exactly 100. Keyed by the operator-chosen <see cref="ModelId"/>.
/// </summary>
public class ModelPortfolio : IAggregateRoot
{
    private readonly List<ModelPortfolioFund> _funds = new();

    // Required by EF Core
#pragma warning disable CS8618
    private ModelPortfolio() { }
#pragma warning restore CS8618

    public ModelPortfolio(string modelId, string name, IEnumerable<ModelPortfolioFund> funds)
    {
        ModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        Replace(name, funds);
    }

    /// <summary>The operator-chosen id of the model. Primary key.</summary>
    public string ModelId { get; private set; }

    public string Name { get; private set; }

    public IReadOnlyCollection<ModelPortfolioFund> Funds => _funds.AsReadOnly();

    /// <summary>The Upvest allocation this model maps to, once created at Upvest.</summary>
    public Guid? UpvestAllocationId { get; private set; }

    /// <summary>Idempotency key for creating the Upvest allocation; reused on resend.</summary>
    public Guid CreateAllocationIdempotencyKey { get; private set; } = Guid.NewGuid();

    public void Replace(string name, IEnumerable<ModelPortfolioFund> funds)
    {
        Name = Guard.Against.NullOrEmpty(name, nameof(name));
        Guard.Against.Null(funds, nameof(funds));

        _funds.Clear();
        _funds.AddRange(funds);

        Guard.Against.InvalidInput(_funds, nameof(funds), f => f.Count > 0,
            "A model portfolio must contain at least one fund.");
    }

    public void SetUpvestAllocationId(Guid allocationId) => UpvestAllocationId = allocationId;

    /// <summary>Sum of the fund weights; must equal exactly 100 for a valid model.</summary>
    public decimal TotalWeight => _funds.Sum(f => f.Weight);
}
