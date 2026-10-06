namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Lifecycle of a shopper's enrolment as an investor with Upvest.
/// Mirrors the outcome of the KYC check Upvest performs before accepting the investor.
/// </summary>
public enum EnrolmentStatus
{
    /// <summary>Upvest has not yet decided; the shopper cannot invest.</summary>
    Pending = 0,

    /// <summary>Upvest has accepted the shopper as an investor; investing is enabled.</summary>
    Active = 1,

    /// <summary>Upvest declined to take the shopper on as an investor.</summary>
    Rejected = 2
}
