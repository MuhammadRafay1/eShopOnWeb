namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>What a shopper has set aside and not yet invested, and the total invested so far.</summary>
public record BalanceSummary(decimal PendingAmount, decimal InvestedAmount);
