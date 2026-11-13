using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A model portfolio defined by the shop operator: a named mix of funds with a target
/// weight per fund. The weights add up to exactly 100.
/// <para>
/// <see cref="ModelId"/> is the operator-supplied business identifier used on the API
/// (<c>PUT /api/portfolio/models/{modelId}</c>); the inherited <see cref="BaseEntity.Id"/>
/// is the internal persistence key.
/// </para>
/// </summary>
public class ModelPortfolio : BaseEntity, IAggregateRoot
{
    /// <summary>Operator-chosen model identifier (the <c>{modelId}</c> route value).</summary>
    public int ModelId { get; private set; }

    public string Name { get; private set; }

    /// <summary>The Upvest allocation id backing this model, once created at Upvest.</summary>
    public string? UpvestAllocationId { get; private set; }

    private readonly List<ModelFund> _funds = new();
    public IReadOnlyCollection<ModelFund> Funds => _funds.AsReadOnly();

#pragma warning disable CS8618 // Required by Entity Framework
    private ModelPortfolio() { }
#pragma warning restore CS8618

    public ModelPortfolio(int modelId, string name, IEnumerable<ModelFund> funds)
    {
        ModelId = modelId;
        Name = Guard.Against.NullOrEmpty(name, nameof(name));
        Replace(name, funds);
    }

    /// <summary>Replace the model's name and funds (used by the create-or-replace PUT).</summary>
    public void Replace(string name, IEnumerable<ModelFund> funds)
    {
        Name = Guard.Against.NullOrEmpty(name, nameof(name));
        var list = Guard.Against.Null(funds, nameof(funds)).ToList();
        Guard.Against.NullOrEmpty(list, nameof(funds));

        var total = list.Sum(f => f.Weight);
        if (total != 100m)
        {
            throw new InvalidModelPortfolioException(
                $"Model fund weights must add up to exactly 100, but they add up to {total}.");
        }

        if (list.Select(f => f.InstrumentId).Distinct().Count() != list.Count)
        {
            throw new InvalidModelPortfolioException("A model portfolio must not list the same fund twice.");
        }

        _funds.Clear();
        _funds.AddRange(list);
    }

    public void SetUpvestAllocation(string allocationId)
        => UpvestAllocationId = Guard.Against.NullOrEmpty(allocationId, nameof(allocationId));
}
