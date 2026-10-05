namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// The outcome of an investment placed on the shopper's behalf at Upvest.
/// </summary>
public enum InvestmentStatus
{
    /// <summary>Placed at Upvest; the final outcome is not yet known.</summary>
    Pending = 0,

    /// <summary>The investment settled at Upvest (the order was filled).</summary>
    Settled = 1,

    /// <summary>The investment failed at Upvest (the order was cancelled/rejected).</summary>
    Failed = 2
}
