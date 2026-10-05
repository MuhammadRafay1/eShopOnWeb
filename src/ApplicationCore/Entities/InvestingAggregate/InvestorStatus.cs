namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a shopper's enrolment as an Upvest investor has got to.
/// </summary>
public enum InvestorStatus
{
    /// <summary>Submitted to Upvest; awaiting acceptance (user activation).</summary>
    Pending = 0,

    /// <summary>Accepted by Upvest; the shopper can now invest.</summary>
    Active = 1,

    /// <summary>Upvest declined to take the shopper on.</summary>
    Rejected = 2
}
