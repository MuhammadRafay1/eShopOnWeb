namespace Microsoft.eShopWeb.ApplicationCore.Portfolios;

/// <summary>
/// Lifecycle of an investor sign-up, as reflected by Upvest.
/// </summary>
public enum InvestorStatus
{
    /// <summary>Upvest has not yet accepted the shopper / their account is not ready.</summary>
    Pending,

    /// <summary>Upvest has accepted the shopper and their account is ready to hold a portfolio.</summary>
    Active,

    /// <summary>Upvest will not take the shopper on.</summary>
    Rejected
}

/// <summary>
/// Lifecycle of a pay-in, as reflected by Upvest.
/// </summary>
public enum PaymentStatus
{
    /// <summary>The outcome at Upvest is not yet known.</summary>
    Pending,

    /// <summary>The money was invested across the model's funds at Upvest.</summary>
    Invested,

    /// <summary>The pay-in failed at Upvest.</summary>
    Failed
}

/// <summary>
/// Lifecycle of a realignment, as reflected by Upvest.
/// </summary>
public enum RealignmentStatus
{
    /// <summary>The outcome at Upvest is not yet known.</summary>
    Pending,

    /// <summary>All affected portfolios were realigned at Upvest.</summary>
    Completed,

    /// <summary>The realignment failed at Upvest.</summary>
    Failed
}
