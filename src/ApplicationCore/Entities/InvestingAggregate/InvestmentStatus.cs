namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a single investment has got to. Surfaced to the API as "pending" / "settled" / "failed".
/// </summary>
public enum InvestmentStatus
{
    /// <summary>The investment has been placed but its outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>The order filled at Upvest — the money is invested.</summary>
    Settled = 1,

    /// <summary>The order did not go through at Upvest.</summary>
    Failed = 2
}
