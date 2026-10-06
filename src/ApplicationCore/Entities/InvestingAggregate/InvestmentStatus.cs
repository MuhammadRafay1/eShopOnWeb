namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where an individual investment has got to at Upvest.
/// </summary>
public enum InvestmentStatus
{
    /// <summary>The investment has been placed but its outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>Upvest has fully executed the investment.</summary>
    Settled = 1,

    /// <summary>Upvest could not execute the investment.</summary>
    Failed = 2
}
