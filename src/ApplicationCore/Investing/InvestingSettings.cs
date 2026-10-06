namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// Settings that govern how spare change is invested. The instrument id is bound from
/// configuration (<c>Upvest:InstrumentId</c>); the rest are fixed by the product rules.
/// </summary>
public class InvestingSettings
{
    /// <summary>The fund (instrument) that set-aside change is invested in. Bound from <c>Upvest:InstrumentId</c>.</summary>
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>The currency all amounts are expressed in. Catalog prices are euro amounts.</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Once the set-aside balance reaches this amount, the whole balance is invested.</summary>
    public decimal InvestmentThreshold { get; set; } = 10m;
}
