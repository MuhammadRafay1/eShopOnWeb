namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a single investment has got to at Upvest.
/// </summary>
public enum InvestmentStatus
{
    /// <summary>The investment's outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>The investment completed at Upvest.</summary>
    Settled = 1,

    /// <summary>The investment did not complete at Upvest.</summary>
    Failed = 2
}
