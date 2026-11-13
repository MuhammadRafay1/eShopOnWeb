namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Outcome of a realignment at Upvest. <see cref="Pending"/> until the outcome at Upvest is
/// known, then <see cref="Completed"/> or <see cref="Failed"/>.
/// </summary>
public enum RealignmentStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2
}
