using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// One fund within a model portfolio: the fund's ISIN and its target weight (a percentage).
/// A child of <see cref="ModelPortfolio"/>; not an aggregate root in its own right.
/// </summary>
public class ModelFund : BaseEntity
{
#pragma warning disable CS8618 // Required by Entity Framework
    private ModelFund() { }
#pragma warning restore CS8618

    public ModelFund(string instrumentId, decimal weight)
    {
        Guard.Against.NullOrEmpty(instrumentId, nameof(instrumentId));
        InstrumentId = instrumentId;
        Weight = weight;
    }

    /// <summary>The fund's ISIN.</summary>
    public string InstrumentId { get; private set; }

    /// <summary>Target weight as a percentage (the weights of a model add up to exactly 100).</summary>
    public decimal Weight { get; private set; }
}
