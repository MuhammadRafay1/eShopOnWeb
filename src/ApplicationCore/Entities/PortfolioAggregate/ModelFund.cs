using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// One fund within a model portfolio: the fund's ISIN and its target weight (a percentage).
/// </summary>
public class ModelFund : BaseEntity
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

    /// <summary>Target weight as a percentage (0–100). The weights of a model sum to 100.</summary>
    public decimal Weight { get; private set; }
}
