using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-defined model portfolio: a named mix of funds with a target weight per fund,
/// backed by a portfolio allocation at Upvest. Investors choose a model for their portfolio to
/// follow.
/// </summary>
public class ModelPortfolio : BaseEntity, IAggregateRoot
{
    /// <summary>Operator-chosen identifier for the model (the <c>{modelId}</c> route value).</summary>
    public string ModelId { get; private set; }

    public string Name { get; private set; }

    /// <summary>Identifier of the backing portfolio allocation at Upvest.</summary>
    public string? UpvestAllocationId { get; private set; }

    private readonly List<ModelFund> _funds = new();
    public IReadOnlyCollection<ModelFund> Funds => _funds.AsReadOnly();

#pragma warning disable CS8618 // Required by Entity Framework
    private ModelPortfolio() { }
#pragma warning restore CS8618

    public ModelPortfolio(string modelId, string name, IEnumerable<ModelFund> funds)
    {
        Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        ModelId = modelId;
        SetDefinition(name, funds);
    }

    /// <summary>Replace the model's name and funds. Weights must sum to exactly 100.</summary>
    public void SetDefinition(string name, IEnumerable<ModelFund> funds)
    {
        Guard.Against.NullOrEmpty(name, nameof(name));
        Guard.Against.Null(funds, nameof(funds));

        var fundList = funds.ToList();
        Guard.Against.NullOrEmpty(fundList, nameof(funds));

        var total = fundList.Sum(f => f.Weight);
        if (total != 100m)
        {
            throw new System.ArgumentException(
                $"The weights of a model must add up to exactly 100 (got {total}).", nameof(funds));
        }

        Name = name;
        _funds.Clear();
        _funds.AddRange(fundList);
    }

    public void SetUpvestAllocation(string allocationId)
    {
        Guard.Against.NullOrEmpty(allocationId, nameof(allocationId));
        UpvestAllocationId = allocationId;
    }
}
