using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// One fund inside a model portfolio: the fund's ISIN and its target weight (a percentage).
/// </summary>
public class ModelFund : BaseEntity
{
    /// <summary>The fund's ISIN (International Securities Identification Number).</summary>
    public string InstrumentId { get; private set; }

    /// <summary>Target weight as a percentage (0–100). The weights of a model sum to exactly 100.</summary>
    public decimal Weight { get; private set; }

    public int ModelPortfolioId { get; private set; }

#pragma warning disable CS8618 // Required by Entity Framework
    private ModelFund() { }
#pragma warning restore CS8618

    public ModelFund(string instrumentId, decimal weight)
    {
        InstrumentId = Guard.Against.NullOrEmpty(instrumentId, nameof(instrumentId));
        Weight = Guard.Against.OutOfRange(weight, nameof(weight), 0m, 100m);
    }
}
