namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a shopper's enrolment as an Upvest investor has got to.
/// </summary>
public enum InvestorStatus
{
    /// <summary>Upvest has not yet accepted the shopper as an investor.</summary>
    Pending = 0,

    /// <summary>Upvest has accepted the shopper; they can hold investments.</summary>
    Active = 1,

    /// <summary>Upvest declined to take the shopper on.</summary>
    Rejected = 2
}
