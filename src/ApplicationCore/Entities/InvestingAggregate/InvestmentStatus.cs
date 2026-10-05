namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// The lifecycle of a single investment, mirroring the outcome of the
/// backing order at Upvest.
/// </summary>
public enum InvestmentStatus
{
    /// <summary>Placed with Upvest; final outcome not yet known.</summary>
    Pending = 0,

    /// <summary>The order filled at Upvest.</summary>
    Settled = 1,

    /// <summary>The order was cancelled/rejected at Upvest.</summary>
    Failed = 2
}
