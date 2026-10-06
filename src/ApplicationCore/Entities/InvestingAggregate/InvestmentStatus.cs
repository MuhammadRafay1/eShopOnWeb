namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Lifecycle of a single investment placed with Upvest, reflecting the state of the
/// underlying Upvest order.
/// </summary>
public enum InvestmentStatus
{
    /// <summary>The order has been placed but its outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>The Upvest order has been fully executed.</summary>
    Settled = 1,

    /// <summary>The Upvest order was cancelled or rejected and did not execute.</summary>
    Failed = 2
}
