namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a shopper's enrolment with the investment provider has got to.
/// Surfaced to callers as the lower-case wire values <c>pending</c>, <c>active</c>, <c>rejected</c>.
/// </summary>
public enum EnrolmentStatus
{
    /// <summary>The provider has the shopper but has not yet accepted them as an investor.</summary>
    Pending = 0,

    /// <summary>The provider has accepted the shopper; they may invest.</summary>
    Active = 1,

    /// <summary>The provider will not take the shopper on.</summary>
    Rejected = 2
}
