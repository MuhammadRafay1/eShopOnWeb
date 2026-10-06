namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a shopper's enrolment as an investor with Upvest has got to.
/// </summary>
public enum InvestorStatus
{
    /// <summary>Submitted to Upvest, not yet accepted.</summary>
    Pending = 0,

    /// <summary>Accepted by Upvest; the shopper can invest.</summary>
    Active = 1,

    /// <summary>Upvest declined to take the shopper on.</summary>
    Rejected = 2
}
