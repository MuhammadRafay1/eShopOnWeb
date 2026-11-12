namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Where a realignment has got to at Upvest: <c>pending</c> until the outcome is known, then
/// <c>completed</c> or <c>failed</c>.
/// </summary>
public enum RealignmentStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2
}
