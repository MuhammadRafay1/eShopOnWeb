namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a single investment has got to. Mirrors the outcome of the backing order at Upvest.
/// </summary>
public enum InvestmentStatus
{
    /// <summary>The backing order has been placed; its outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>The backing order settled at Upvest.</summary>
    Settled = 1,

    /// <summary>The backing order failed at Upvest; the money has been returned to the set-aside balance.</summary>
    Failed = 2
}
