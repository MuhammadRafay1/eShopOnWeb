namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a single investment has got to at Upvest.
/// </summary>
public enum InvestmentStatus
{
    /// <summary>Placed with Upvest; the outcome is not yet known.</summary>
    Pending = 0,

    /// <summary>The investment was executed and settled at Upvest.</summary>
    Settled = 1,

    /// <summary>The investment did not go through at Upvest.</summary>
    Failed = 2
}
