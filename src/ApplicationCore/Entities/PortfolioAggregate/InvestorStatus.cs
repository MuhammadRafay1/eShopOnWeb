namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Lifecycle of a shopper's investor sign-up, as reflected by Upvest.
/// </summary>
public enum InvestorStatus
{
    /// <summary>Upvest has not yet accepted the shopper / the account is not yet ready.</summary>
    Pending = 0,

    /// <summary>Upvest has accepted the shopper and their account is ready to hold a portfolio.</summary>
    Active = 1,

    /// <summary>Upvest will not take the shopper on.</summary>
    Rejected = 2
}
