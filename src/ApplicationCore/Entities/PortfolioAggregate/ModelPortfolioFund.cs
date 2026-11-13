using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// One fund in a model portfolio: the fund's ISIN and its target weight (a percentage).
/// </summary>
public class ModelPortfolioFund
{
    // Required by EF Core
#pragma warning disable CS8618
    private ModelPortfolioFund() { }
#pragma warning restore CS8618

    public ModelPortfolioFund(string instrumentId, decimal weight)
    {
        InstrumentId = Guard.Against.NullOrEmpty(instrumentId, nameof(instrumentId));
        Weight = weight;
    }

    /// <summary>The fund's ISIN.</summary>
    public string InstrumentId { get; private set; }

    /// <summary>Target weight for this fund, as a percentage.</summary>
    public decimal Weight { get; private set; }
}
