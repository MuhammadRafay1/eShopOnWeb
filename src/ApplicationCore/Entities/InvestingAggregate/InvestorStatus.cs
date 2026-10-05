namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a shopper's enrolment as an investor has got to. Surfaced to callers as
/// <c>pending</c>, <c>active</c> or <c>rejected</c>.
/// </summary>
public enum InvestorStatus
{
    /// <summary>Upvest has not yet accepted the shopper as an investor.</summary>
    Pending = 0,

    /// <summary>Upvest has accepted the shopper; they may invest.</summary>
    Active = 1,

    /// <summary>Upvest declined to take the shopper on.</summary>
    Rejected = 2
}
