using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>A single fund within a <see cref="ModelPortfolio"/>: its ISIN and target weight (percent).</summary>
public class ModelFund
{
#pragma warning disable CS8618 // Required by Entity Framework
    private ModelFund() { }
#pragma warning restore CS8618

    public ModelFund(string instrumentId, decimal weight)
    {
        InstrumentId = Guard.Against.NullOrEmpty(instrumentId, nameof(instrumentId));
        Weight = Guard.Against.NegativeOrZero(weight, nameof(weight));
    }

    /// <summary>The fund's ISIN.</summary>
    public string InstrumentId { get; private set; }

    /// <summary>Target weight as a percentage (model weights sum to 100).</summary>
    public decimal Weight { get; private set; }
}
