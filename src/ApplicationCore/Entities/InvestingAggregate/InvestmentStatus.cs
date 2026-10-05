namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a single investment has got to. Mirrors the outcome of the buy order at Upvest.
/// </summary>
public enum InvestmentStatus
{
    /// <summary>The investment has been started but its outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>Upvest filled the order; the money is invested.</summary>
    Settled = 1,

    /// <summary>Upvest did not fill the order; the money was not invested.</summary>
    Failed = 2
}
