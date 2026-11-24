using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// One fund within a model portfolio: a fund identified by its ISIN and its target weight
/// (a percentage) within the model.
/// </summary>
public class ModelFund : BaseEntity
{
    /// <summary>The fund's ISIN (Upvest instrument id of type ISIN).</summary>
    public string InstrumentId { get; private set; }

    /// <summary>Target weight of this fund within the model, as a percentage.</summary>
    public decimal Weight { get; private set; }

#pragma warning disable CS8618 // Required by Entity Framework
    private ModelFund() { }
#pragma warning restore CS8618

    public ModelFund(string instrumentId, decimal weight)
    {
        Guard.Against.NullOrEmpty(instrumentId, nameof(instrumentId));
        Guard.Against.NegativeOrZero(weight, nameof(weight));
        InstrumentId = instrumentId;
        Weight = weight;
    }
}
