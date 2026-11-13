namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Lifecycle of a realignment (rebalancing) execution, as reflected by Upvest.
/// </summary>
public enum RealignmentStatus
{
    /// <summary>The outcome of the realignment at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>The realignment completed at Upvest.</summary>
    Completed = 1,

    /// <summary>The realignment failed at Upvest.</summary>
    Failed = 2
}
