using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A single fund within a model portfolio: the fund's ISIN and its target weight (a percentage).
/// Mapped as an EF owned entity of <see cref="ModelPortfolio"/>.
/// </summary>
public class ModelFund
{
    public string InstrumentId { get; private set; }

    /// <summary>Target weight as a percentage (0–100).</summary>
    public decimal Weight { get; private set; }

    private ModelFund()
    {
        InstrumentId = default!;
    }

    public ModelFund(string instrumentId, decimal weight)
    {
        InstrumentId = Guard.Against.NullOrEmpty(instrumentId, nameof(instrumentId));
        if (weight <= 0m || weight > 100m)
            throw new System.ArgumentOutOfRangeException(nameof(weight), weight, "Weight must be a percentage in (0, 100].");
        Weight = weight;
    }
}
