namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Lifecycle of a shopper's enrolment as an investor with Upvest.
/// </summary>
public enum EnrolmentStatus
{
    /// <summary>Submitted to Upvest; not yet accepted as an investor.</summary>
    Pending = 0,

    /// <summary>Accepted by Upvest; the shopper can invest.</summary>
    Active = 1,

    /// <summary>Upvest declined to take the shopper on as an investor.</summary>
    Rejected = 2
}
