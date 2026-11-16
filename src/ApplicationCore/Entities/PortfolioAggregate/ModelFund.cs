using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A single fund in a model portfolio, with its target weight.
/// </summary>
public class ModelFund : BaseEntity
{
    /// <summary>The fund's ISIN (Upvest instrument id, type ISIN).</summary>
    public string InstrumentId { get; private set; }

    /// <summary>Target weight as a percentage (model weights sum to exactly 100).</summary>
    public decimal Weight { get; private set; }

    public int ModelPortfolioId { get; private set; }

    public ModelFund(string instrumentId, decimal weight)
    {
        InstrumentId = Guard.Against.NullOrEmpty(instrumentId, nameof(instrumentId));
        Weight = weight;
    }

#pragma warning disable CS8618 // Required by Entity Framework
    private ModelFund() { }
#pragma warning restore CS8618
}
