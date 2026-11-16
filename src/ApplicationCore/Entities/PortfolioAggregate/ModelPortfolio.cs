using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-defined model portfolio: a named mix of funds with a target weight per fund.
/// Mirrored at Upvest as an "allocation"; <see cref="UpvestAllocationId"/> links the two once the
/// allocation has been created. <see cref="ModelId"/> is the operator-supplied identifier used in
/// the API routes.
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
        AllocationCreateKey = Guid.NewGuid();
        Replace(name, funds);
    }

    /// <summary>Operator-supplied identifier (route <c>{modelId}</c>). Unique.</summary>
    public string ModelId { get; private set; }

    public string Name { get; private set; }

    /// <summary>The Upvest allocation id this model maps to, once created.</summary>
    public Guid? UpvestAllocationId { get; private set; }

    /// <summary>Idempotency key for the allocation create, generated once with the model.</summary>
    public Guid AllocationCreateKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<ModelFund> _funds = new();
    public IReadOnlyCollection<ModelFund> Funds => _funds.AsReadOnly();

    /// <summary>Replace the name and the full set of funds (the "replace" half of PUT).</summary>
    public void Replace(string name, IEnumerable<ModelFund> funds)
    {
        Guard.Against.NullOrEmpty(name, nameof(name));
        Guard.Against.Null(funds, nameof(funds));
        var materialised = funds.ToList();
        Guard.Against.NullOrEmpty(materialised, nameof(funds));

        Name = name;
        _funds.Clear();
        _funds.AddRange(materialised);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetUpvestAllocationId(Guid allocationId) => UpvestAllocationId = allocationId;
}
