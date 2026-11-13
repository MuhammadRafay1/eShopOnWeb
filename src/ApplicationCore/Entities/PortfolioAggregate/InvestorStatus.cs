namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Lifecycle status of a shopper's investor sign-up, mirroring the state at Upvest.
/// </summary>
public enum InvestorStatus
{
    /// <summary>Upvest has not yet accepted the shopper / their account is not yet ready.</summary>
    Pending = 0,

    /// <summary>Upvest has accepted the shopper and their account is ready to hold a portfolio.</summary>
    Active = 1,

    /// <summary>Upvest has declined to take the shopper on.</summary>
    Rejected = 2
}
