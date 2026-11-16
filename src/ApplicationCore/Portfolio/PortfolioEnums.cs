namespace Microsoft.eShopWeb.ApplicationCore.Portfolio;

/// <summary>
/// Lifecycle status of an investor sign-up, mirroring the state at Upvest.
/// </summary>
public enum InvestorStatus
{
    /// <summary>Upvest has not yet accepted the shopper / the account is not yet ready.</summary>
    Pending,
    /// <summary>Upvest accepted the shopper and the account is ready to hold a portfolio.</summary>
    Active,
    /// <summary>Upvest will not take the shopper on.</summary>
    Rejected
}

/// <summary>
/// Lifecycle status of a pay-in, mirroring the portfolio order outcome at Upvest.
/// </summary>
public enum PaymentStatus
{
    /// <summary>The payment outcome at Upvest is not yet known.</summary>
    Pending,
    /// <summary>The money was credited and invested across the model's funds.</summary>
    Invested,
    /// <summary>The payment failed at Upvest.</summary>
    Failed
}

/// <summary>
/// Lifecycle status of a realignment, mirroring the rebalancing execution outcome at Upvest.
/// </summary>
public enum RealignmentStatus
{
    /// <summary>The realignment outcome at Upvest is not yet known.</summary>
    Pending,
    /// <summary>The realignment completed at Upvest.</summary>
    Completed,
    /// <summary>The realignment failed at Upvest.</summary>
    Failed
}
