using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-defined model portfolio: a named mix of funds with a target weight per fund.
/// Mirrors an Upvest portfolio allocation (identified by <see cref="UpvestAllocationId"/>).
/// </summary>
public class ModelPortfolio : BaseEntity, IAggregateRoot
{
    /// <summary>The operator-facing identifier supplied on <c>PUT /api/portfolio/models/{modelId}</c>.</summary>
    public string ModelId { get; private set; }

    public string Name { get; private set; }

    /// <summary>The id of the backing Upvest allocation, once created.</summary>
    public string? UpvestAllocationId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<ModelFund> _funds = new();
    public IReadOnlyCollection<ModelFund> Funds => _funds.AsReadOnly();

    private ModelPortfolio()
    {
        ModelId = default!;
        Name = default!;
    }

    public ModelPortfolio(string modelId, string name, IEnumerable<ModelFund> funds)
    {
        ModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        Name = Guard.Against.NullOrEmpty(name, nameof(name));
        SetFunds(funds);
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    /// <summary>Replace the model's name and funds (used by the create-or-replace PUT).</summary>
    public void Replace(string name, IEnumerable<ModelFund> funds)
    {
        Name = Guard.Against.NullOrEmpty(name, nameof(name));
        SetFunds(funds);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetUpvestAllocationId(string allocationId)
    {
        UpvestAllocationId = Guard.Against.NullOrEmpty(allocationId, nameof(allocationId));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void SetFunds(IEnumerable<ModelFund> funds)
    {
        Guard.Against.Null(funds, nameof(funds));
        var list = funds.ToList();
        Guard.Against.NullOrEmpty(list, nameof(funds));

        var total = list.Sum(f => f.Weight);
        // Weights are percentages and must add up to exactly 100.
        if (total != 100m)
            throw new ArgumentException($"Fund weights must sum to exactly 100 (got {total}).", nameof(funds));

        if (list.Select(f => f.InstrumentId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != list.Count)
            throw new ArgumentException("A model portfolio may not list the same instrument twice.", nameof(funds));

        _funds.Clear();
        _funds.AddRange(list);
    }
}
