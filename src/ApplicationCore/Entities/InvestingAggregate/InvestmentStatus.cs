namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// The lifecycle of a single investment of set-aside change, mirroring what
/// actually happened to it at Upvest.
/// </summary>
public enum InvestmentStatus
{
    /// <summary>The investment has been placed but its outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>The investment settled at Upvest.</summary>
    Settled = 1,

    /// <summary>The investment failed at Upvest.</summary>
    Failed = 2
}
