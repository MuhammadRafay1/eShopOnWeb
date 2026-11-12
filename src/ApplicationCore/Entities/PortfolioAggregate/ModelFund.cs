using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// One fund in a model portfolio: a fund identified by its ISIN and its target weight (a percentage).
/// An owned value object of <see cref="ModelPortfolio"/>.
/// </summary>
public class ModelFund
{
#pragma warning disable CS8618 // Required by Entity Framework
    private ModelFund() { }
#pragma warning restore CS8618

    public ModelFund(string instrumentId, decimal weight)
    {
        InstrumentId = Guard.Against.NullOrEmpty(instrumentId, nameof(instrumentId));
        Weight = Guard.Against.OutOfRange(weight, nameof(weight), 0m, 100m);
    }

    /// <summary>The fund's ISIN.</summary>
    public string InstrumentId { get; private set; }

    /// <summary>Target weight as a percentage.</summary>
    public decimal Weight { get; private set; }
}
