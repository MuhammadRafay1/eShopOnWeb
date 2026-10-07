namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a shopper's enrolment as an investor has got to. A shopper can only
/// invest their change once Upvest has accepted them (<see cref="Active"/>).
/// </summary>
public enum EnrolmentStatus
{
    /// <summary>Upvest has not yet accepted the shopper as an investor.</summary>
    Pending = 0,

    /// <summary>Upvest has accepted the shopper; they can hold investments.</summary>
    Active = 1,

    /// <summary>Upvest will not take the shopper on as an investor.</summary>
    Rejected = 2
}
