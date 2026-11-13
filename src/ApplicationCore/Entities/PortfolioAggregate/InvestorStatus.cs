namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// The lifecycle of a shopper's sign-up as an investor, as reflected by Upvest.
/// <see cref="Pending"/> until Upvest has accepted the shopper and their account is ready,
/// then <see cref="Active"/>, or <see cref="Rejected"/> if Upvest will not take them on.
/// </summary>
public enum InvestorStatus
{
    Pending = 0,
    Active = 1,
    Rejected = 2
}
