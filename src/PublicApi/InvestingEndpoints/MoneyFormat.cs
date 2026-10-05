namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

internal static class MoneyFormat
{
    /// <summary>Euro cents as a decimal with two decimal places (e.g. 1070 -&gt; 10.70).</summary>
    public static decimal Euros(long cents) => (cents / 100m) + 0.00m;
}
