namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// The fixed status vocabularies reported by the portfolio API. Every value here reflects what
/// has actually happened at Upvest, learned through the shop's own calls to Upvest.
/// </summary>
public static class PortfolioStatuses
{
    // Shared "awaiting outcome" value.
    public const string Pending = "pending";

    // Investor sign-up.
    public const string Active = "active";
    public const string Rejected = "rejected";

    // Payment.
    public const string Invested = "invested";
    public const string Failed = "failed";

    // Realignment.
    public const string Completed = "completed";
    // (Realignment also uses Failed above.)
}
