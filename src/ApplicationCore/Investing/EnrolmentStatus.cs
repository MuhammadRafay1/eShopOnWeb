namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// Where a shopper's investor enrolment has got to with Upvest.
/// </summary>
public enum EnrolmentStatus
{
    /// <summary>Submitted to Upvest; not yet accepted as an investor.</summary>
    Pending = 0,

    /// <summary>Accepted by Upvest; the shopper can hold investments.</summary>
    Active = 1,

    /// <summary>Upvest will not take the shopper on as an investor.</summary>
    Rejected = 2
}
