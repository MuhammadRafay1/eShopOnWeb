namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>Fixed business rules for "Invest your change".</summary>
public static class InvestingPolicy
{
    /// <summary>
    /// Once the set-aside balance reaches this many euros, the whole balance is invested and the balance
    /// starts again from zero.
    /// </summary>
    public const decimal InvestmentThresholdEuros = 10m;
}
