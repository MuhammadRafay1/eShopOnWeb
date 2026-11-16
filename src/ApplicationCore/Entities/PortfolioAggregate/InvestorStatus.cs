namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Lifecycle of a shopper's investor sign-up at Upvest.
/// </summary>
public enum InvestorStatus
{
    /// <summary>Upvest has not yet accepted the shopper and/or their account is not ready.</summary>
    Pending = 0,

    /// <summary>Upvest has accepted the shopper and their managed-portfolio account is ready.</summary>
    Active = 1,

    /// <summary>Upvest will not take the shopper on.</summary>
    Rejected = 2
}
