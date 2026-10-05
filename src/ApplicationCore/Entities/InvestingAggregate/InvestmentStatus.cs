namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Lifecycle of a single investment of set-aside change into the fund at Upvest.
/// </summary>
public enum InvestmentStatus
{
    /// <summary>Order placed (or about to be placed) at Upvest; outcome not yet known.</summary>
    Pending = 0,

    /// <summary>The investment order was filled at Upvest.</summary>
    Settled = 1,

    /// <summary>The investment order was cancelled/rejected at Upvest.</summary>
    Failed = 2
}
