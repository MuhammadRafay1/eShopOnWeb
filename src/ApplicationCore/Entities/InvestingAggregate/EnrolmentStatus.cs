namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a shopper's enrolment as an Upvest investor has got to.
/// </summary>
public enum EnrolmentStatus
{
    /// <summary>Submitted to Upvest, awaiting acceptance (regulatory checks in progress).</summary>
    Pending = 0,

    /// <summary>Upvest has accepted the shopper as an investor; they can now invest.</summary>
    Active = 1,

    /// <summary>Upvest will not take the shopper on.</summary>
    Rejected = 2
}
