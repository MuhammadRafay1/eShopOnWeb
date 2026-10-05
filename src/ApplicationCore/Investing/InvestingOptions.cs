namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// Tunables for the invest-your-change capability. The instrument comes from the
/// <c>Upvest:InstrumentId</c> configuration; the threshold is a business rule.
/// </summary>
public class InvestingOptions
{
    /// <summary>ISIN of the fund the set-aside change is invested in (from <c>Upvest:InstrumentId</c>).</summary>
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>Once the set-aside balance reaches this many euros, the whole balance is invested.</summary>
    public decimal InvestmentThresholdEuros { get; set; } = 10m;

    public long InvestmentThresholdCents => (long)(InvestmentThresholdEuros * 100m);
}
