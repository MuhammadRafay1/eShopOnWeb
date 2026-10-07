namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a shopper's enrolment as an Upvest investor has got to.
/// </summary>
public enum EnrolmentStatus
{
    /// <summary>Submitted to Upvest; not yet accepted.</summary>
    Pending = 0,

    /// <summary>Accepted by Upvest; the shopper may now invest.</summary>
    Active = 1,

    /// <summary>Upvest will not take the shopper on.</summary>
    Rejected = 2
}
