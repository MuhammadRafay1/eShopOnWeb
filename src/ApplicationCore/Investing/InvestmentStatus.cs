namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// Lifecycle of a single investment of a shopper's set-aside balance into the fund.
/// </summary>
public enum InvestmentStatus
{
    /// <summary>Created from the set-aside balance; the buy order has not yet been placed at Upvest.</summary>
    Created = 0,

    /// <summary>A buy order has been placed at Upvest and its outcome is not yet known.</summary>
    Placed = 1,

    /// <summary>The order filled at Upvest; the fund units are held on the shopper's behalf.</summary>
    Settled = 2,

    /// <summary>The order did not complete at Upvest; the amount has been returned to the set-aside balance.</summary>
    Failed = 3
}
