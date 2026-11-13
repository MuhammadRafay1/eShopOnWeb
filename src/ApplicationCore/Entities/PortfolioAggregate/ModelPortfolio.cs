using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-defined model portfolio: a named mix of funds with a target weight per fund.
/// Identified to callers by a stable business key (<see cref="ModelId"/>) and backed by an
/// Upvest allocation (<see cref="UpvestAllocationId"/>).
/// </summary>
public class ModelPortfolio : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private ModelPortfolio() { }
#pragma warning restore CS8618

    public ModelPortfolio(string modelId, string name, IEnumerable<ModelFund> funds, string? upvestAllocationId = null)
    {
        Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        ModelId = modelId;
        CreatedAt = DateTimeOffset.UtcNow;
        Replace(name, funds, upvestAllocationId);
    }

    /// <summary>Caller-chosen stable identifier for the model (from the route).</summary>
    public string ModelId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>The Upvest allocation that realises this model, once created.</summary>
    public string? UpvestAllocationId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<ModelFund> _funds = new();
    public IReadOnlyCollection<ModelFund> Funds => _funds.AsReadOnly();

    /// <summary>Create-or-replace semantics: swaps in the new name and complete set of funds.</summary>
    public void Replace(string name, IEnumerable<ModelFund> funds, string? upvestAllocationId)
    {
        Guard.Against.NullOrEmpty(name, nameof(name));
        Guard.Against.Null(funds, nameof(funds));
        var list = funds.ToList();
        Guard.Against.NullOrEmpty(list, nameof(funds));

        Name = name;
        _funds.Clear();
        _funds.AddRange(list);
        if (upvestAllocationId is not null)
        {
            UpvestAllocationId = upvestAllocationId;
        }
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetUpvestAllocationId(string allocationId)
    {
        Guard.Against.NullOrEmpty(allocationId, nameof(allocationId));
        UpvestAllocationId = allocationId;
    }
}
