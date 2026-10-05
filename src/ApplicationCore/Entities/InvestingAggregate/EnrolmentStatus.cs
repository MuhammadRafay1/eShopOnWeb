namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a shopper's enrolment with the investment provider (Upvest) has got to.
/// Surfaced to API callers as the lower-case values "pending", "active" and "rejected".
/// </summary>
public enum EnrolmentStatus
{
    /// <summary>Upvest has not yet accepted the shopper as an investor.</summary>
    Pending = 0,

    /// <summary>Upvest has accepted the shopper; they may now have change invested on their behalf.</summary>
    Active = 1,

    /// <summary>Upvest will not take the shopper on as an investor.</summary>
    Rejected = 2
}
