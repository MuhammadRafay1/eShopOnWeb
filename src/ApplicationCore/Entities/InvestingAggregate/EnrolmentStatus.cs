namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a shopper's enrolment as an Upvest investor has got to.
/// </summary>
public enum EnrolmentStatus
{
    /// <summary>Upvest has not yet accepted the shopper as an investor.</summary>
    Pending = 0,

    /// <summary>Upvest has accepted the shopper; they may now invest their change.</summary>
    Active = 1,

    /// <summary>Upvest will not take the shopper on as an investor.</summary>
    Rejected = 2
}
