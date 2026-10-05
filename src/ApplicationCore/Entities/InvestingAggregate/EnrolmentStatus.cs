namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a shopper's enrolment as an investor has got to.
/// Surfaced to callers (lower-cased) as the <c>status</c> field.
/// </summary>
public enum EnrolmentStatus
{
    /// <summary>Submitted to Upvest, not yet accepted as an investor.</summary>
    Pending = 0,

    /// <summary>Accepted by Upvest; the shopper may now invest.</summary>
    Active = 1,

    /// <summary>Upvest will not take the shopper on.</summary>
    Rejected = 2
}
