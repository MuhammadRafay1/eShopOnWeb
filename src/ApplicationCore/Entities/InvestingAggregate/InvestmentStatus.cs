namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a single investment has got to at Upvest.
/// </summary>
public enum InvestmentStatus
{
    /// <summary>Placed with Upvest; final outcome not yet known.</summary>
    Pending = 0,

    /// <summary>The order filled and settled at Upvest.</summary>
    Settled = 1,

    /// <summary>The order failed or was cancelled at Upvest.</summary>
    Failed = 2
}
