namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a single investment has got to at Upvest.
/// </summary>
public enum InvestmentStatus
{
    /// <summary>Placed; outcome at Upvest not yet known.</summary>
    Pending = 0,

    /// <summary>The order filled and settled at Upvest.</summary>
    Settled = 1,

    /// <summary>The order was cancelled/rejected at Upvest.</summary>
    Failed = 2
}
