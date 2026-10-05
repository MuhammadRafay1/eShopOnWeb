namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>Public enrolment status returned by the API.</summary>
public enum EnrolmentStatus
{
    /// <summary>Upvest has not yet accepted the shopper as an investor.</summary>
    Pending,

    /// <summary>Upvest has accepted the shopper; they can hold investments.</summary>
    Active,

    /// <summary>Upvest will not take the shopper on.</summary>
    Rejected
}
