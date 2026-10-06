namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>Where a single investment has got to, mirroring the outcome of its order at Upvest.</summary>
public enum InvestmentStatus
{
    /// <summary>Placed at Upvest; final outcome not yet known.</summary>
    Pending = 0,

    /// <summary>The investment's order settled (filled) at Upvest.</summary>
    Settled = 1,

    /// <summary>The investment's order failed (cancelled/rejected) at Upvest.</summary>
    Failed = 2,
}
