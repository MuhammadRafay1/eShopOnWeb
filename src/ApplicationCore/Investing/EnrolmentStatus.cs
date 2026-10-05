namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// Where a shopper's enrolment as an investor with Upvest currently stands.
/// </summary>
public enum EnrolmentStatus
{
    /// <summary>Submitted to Upvest; the shopper has not yet been accepted as an investor.</summary>
    Pending = 0,

    /// <summary>Upvest has accepted the shopper; they may now invest.</summary>
    Active = 1,

    /// <summary>Upvest declined to take the shopper on as an investor.</summary>
    Rejected = 2
}
