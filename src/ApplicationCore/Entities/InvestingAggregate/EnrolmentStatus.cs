namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>Where a shopper's enrolment as an Upvest investor has got to.</summary>
public enum EnrolmentStatus
{
    /// <summary>Enrolment submitted; Upvest has not yet accepted the shopper.</summary>
    Pending = 0,

    /// <summary>Upvest has accepted the shopper; they may invest.</summary>
    Active = 1,

    /// <summary>Upvest will not take the shopper on.</summary>
    Rejected = 2,
}
