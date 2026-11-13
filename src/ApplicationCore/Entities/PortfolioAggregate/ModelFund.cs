using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// One fund within a model portfolio: a fund identified by its ISIN and a target weight (a percentage).
/// Owned by <see cref="ModelPortfolio"/>.
/// </summary>
public class ModelFund
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

    /// <summary>Target weight of this fund within the model, as a percentage (model weights sum to 100).</summary>
    public decimal Weight { get; private set; }
}
