using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-defined model portfolio: a named mix of funds with a target weight per fund.
/// The weights add up to exactly 100. Mirrored at Upvest as a portfolio allocation.
/// </summary>
public class ModelPortfolio : BaseEntity, IAggregateRoot
{
    public string Name { get; private set; }

    /// <summary>The Upvest allocation id this model is mirrored to, once created.</summary>
    public string? UpvestAllocationId { get; private set; }

    private readonly List<ModelFund> _funds = new();
    public IReadOnlyCollection<ModelFund> Funds => _funds.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    /// <param name="modelId">The caller-supplied model id (route value).</param>
    public ModelPortfolio(int modelId, string name, IEnumerable<ModelFund> funds)
    {
        Id = modelId;
        Name = Guard.Against.NullOrEmpty(name, nameof(name));
        ReplaceFunds(funds);
    }

#pragma warning disable CS8618 // Required by Entity Framework
    private ModelPortfolio() { }
#pragma warning restore CS8618

    public void Update(string name, IEnumerable<ModelFund> funds)
    {
        Name = Guard.Against.NullOrEmpty(name, nameof(name));
        ReplaceFunds(funds);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetUpvestAllocation(string allocationId)
    {
        UpvestAllocationId = Guard.Against.NullOrEmpty(allocationId, nameof(allocationId));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void ReplaceFunds(IEnumerable<ModelFund> funds)
    {
        var list = funds?.ToList() ?? new List<ModelFund>();
        Guard.Against.InvalidInput(list, nameof(funds), f => f.Count > 0, "A model portfolio must have at least one fund.");

        var total = list.Sum(f => f.Weight);
        Guard.Against.InvalidInput(list, nameof(funds), _ => total == 100m,
            $"Fund weights must add up to exactly 100 (got {total}).");

        var duplicate = list.GroupBy(f => f.InstrumentId).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Fund '{duplicate.Key}' appears more than once in the model.", nameof(funds));
        }

        _funds.Clear();
        _funds.AddRange(list);
    }
}
