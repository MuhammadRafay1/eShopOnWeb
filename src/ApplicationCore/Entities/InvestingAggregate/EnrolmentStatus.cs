namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a shopper's enrolment as an Upvest investor has got to.
/// </summary>
public enum EnrolmentStatus
{
    /// <summary>Submitted to Upvest; not yet accepted as an investor.</summary>
    Pending = 0,

    /// <summary>Accepted by Upvest; the shopper can now invest.</summary>
    Active = 1,

    /// <summary>Upvest will not take the shopper on as an investor.</summary>
    Rejected = 2
}
