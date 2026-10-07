namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// Where a single investment has got to at Upvest.
/// </summary>
public enum InvestmentStatus
{
    /// <summary>Placed at Upvest; its outcome is not yet known.</summary>
    Pending = 0,

    /// <summary>The investment order filled at Upvest.</summary>
    Settled = 1,

    /// <summary>The investment order did not fill (e.g. it was cancelled).</summary>
    Failed = 2
}
