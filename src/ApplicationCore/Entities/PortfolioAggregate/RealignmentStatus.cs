namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Lifecycle status of a realignment, mirroring the outcome at Upvest.
/// </summary>
public enum RealignmentStatus
{
    /// <summary>The realignment's outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>The realignment completed at Upvest.</summary>
    Completed = 1,

    /// <summary>The realignment failed at Upvest.</summary>
    Failed = 2
}
