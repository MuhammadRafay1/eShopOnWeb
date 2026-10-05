namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// The lifecycle of a single investment of a shopper's set-aside change.
/// Surfaced to API callers as "pending", "settled" and "failed".
/// </summary>
public enum InvestmentStatus
{
    /// <summary>The investment has been placed with Upvest but its outcome is not yet known.</summary>
    Pending = 0,

    /// <summary>Upvest has confirmed the investment was executed.</summary>
    Settled = 1,

    /// <summary>The investment did not go through at Upvest; its amount returns to the set-aside balance.</summary>
    Failed = 2
}
