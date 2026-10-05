namespace Microsoft.eShopWeb.ApplicationCore.Services.Investing;

public static class InvestingConstants
{
    /// <summary>Once a shopper's set-aside balance reaches this many euros, the whole balance is invested.</summary>
    public const decimal InvestmentThreshold = 10m;

    /// <summary>Give up (and return the money to the balance) after this many failed provider attempts for one investment.</summary>
    public const int MaxInvestmentAttempts = 5;
}
