namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Lifecycle of a realignment (portfolio rebalancing). Always reflects the outcome at Upvest.
/// </summary>
public enum RealignmentStatus
{
    /// <summary>The outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>Every rebalancing order generated at Upvest has completed.</summary>
    Completed = 1,

    /// <summary>At least one rebalancing order was cancelled at Upvest.</summary>
    Failed = 2
}
