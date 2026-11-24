namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Where a shopper's investor sign-up has got to at Upvest.
/// <see cref="Pending"/> until Upvest has accepted the shopper and their account is ready;
/// <see cref="Active"/> once it is; <see cref="Rejected"/> if Upvest will not take them on.
/// </summary>
public enum InvestorStatus
{
    Pending = 0,
    Active = 1,
    Rejected = 2
}
