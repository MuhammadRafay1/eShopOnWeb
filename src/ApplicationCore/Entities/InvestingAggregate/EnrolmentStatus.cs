namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a shopper's enrolment as an investor has got to. A shopper can only
/// invest once Upvest has accepted them (<see cref="Active"/>).
/// </summary>
public enum EnrolmentStatus
{
    /// <summary>Submitted to Upvest; awaiting acceptance (user/account activation).</summary>
    Pending = 0,

    /// <summary>Accepted by Upvest; the shopper can now invest.</summary>
    Active = 1,

    /// <summary>Upvest declined to take the shopper on as an investor.</summary>
    Rejected = 2
}
