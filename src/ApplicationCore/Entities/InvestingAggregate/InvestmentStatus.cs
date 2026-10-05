namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a single investment has got to. Surfaced to callers as <c>pending</c>,
/// <c>settled</c> or <c>failed</c>, reflecting the outcome at Upvest.
/// </summary>
public enum InvestmentStatus
{
    /// <summary>Placed at Upvest; its outcome is not yet known.</summary>
    Pending = 0,

    /// <summary>Executed at Upvest.</summary>
    Settled = 1,

    /// <summary>Upvest could not execute it.</summary>
    Failed = 2
}
