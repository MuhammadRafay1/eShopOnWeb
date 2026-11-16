using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// One fund inside a model portfolio: the fund's ISIN and its target weight (a percentage).
/// </summary>
public class ModelFund : BaseEntity
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

    /// <summary>The fund's target weight, as a percentage (0–100).</summary>
    public decimal Weight { get; private set; }
}
