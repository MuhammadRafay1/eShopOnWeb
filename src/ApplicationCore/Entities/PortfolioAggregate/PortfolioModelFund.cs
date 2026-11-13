using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// One fund weighting within a <see cref="PortfolioModel"/>: a fund ISIN and its target percentage. An owned
/// child of the model, not an aggregate root of its own.
/// </summary>
public class PortfolioModelFund
{
#pragma warning disable CS8618 // Required by Entity Framework
    private PortfolioModelFund() { }
#pragma warning restore CS8618

    public PortfolioModelFund(string instrumentId, decimal weight)
    {
        InstrumentId = Guard.Against.NullOrEmpty(instrumentId, nameof(instrumentId));
        Weight = weight;
    }

    /// <summary>The fund's ISIN.</summary>
    public string InstrumentId { get; private set; }

    /// <summary>The target weight, as a percentage.</summary>
    public decimal Weight { get; private set; }
}
