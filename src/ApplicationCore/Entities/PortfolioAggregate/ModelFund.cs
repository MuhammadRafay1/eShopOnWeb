using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// One fund within a <see cref="ModelPortfolio"/>: the fund's ISIN and its target weight
/// (a percentage). A value object owned by its model.
/// </summary>
public class ModelFund
{
#pragma warning disable CS8618 // Required by Entity Framework
    private ModelFund() { }
#pragma warning restore CS8618

    public ModelFund(string instrumentId, decimal weight)
    {
        Guard.Against.NullOrEmpty(instrumentId, nameof(instrumentId));
        Guard.Against.OutOfRange(weight, nameof(weight), 0m, 100m);
        InstrumentId = instrumentId;
        Weight = weight;
    }

    /// <summary>The fund's ISIN.</summary>
    public string InstrumentId { get; private set; }

    /// <summary>The target weight as a percentage (0–100).</summary>
    public decimal Weight { get; private set; }
}
