namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a single investment has got to at Upvest.
/// </summary>
public enum InvestmentStatus
{
    /// <summary>The order has been placed but its outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>The order settled at Upvest.</summary>
    Settled = 1,

    /// <summary>The order failed, was cancelled or rejected at Upvest.</summary>
    Failed = 2
}
