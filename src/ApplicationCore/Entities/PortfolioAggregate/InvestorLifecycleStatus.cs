namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// The sign-up lifecycle the API exposes for an investor. Mirrors what actually happened at Upvest:
/// <c>pending</c> until Upvest has accepted the shopper and their account is ready, then <c>active</c>,
/// or <c>rejected</c> if Upvest will not take them on.
/// </summary>
public enum InvestorLifecycleStatus
{
    Pending = 0,
    Active = 1,
    Rejected = 2
}
