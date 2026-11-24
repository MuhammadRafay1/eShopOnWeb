using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>One fund (identified by its ISIN) in a model portfolio and its target weight (percent).</summary>
public class ModelFund : BaseEntity
{
    public string InstrumentId { get; private set; }
    public decimal Weight { get; private set; }

    private ModelFund()
    {
        InstrumentId = string.Empty;
    }

    public ModelFund(string instrumentId, decimal weight)
    {
        InstrumentId = Guard.Against.NullOrEmpty(instrumentId, nameof(instrumentId));
        Weight = Guard.Against.OutOfRange(weight, nameof(weight), 0m, 100m);
    }
}
