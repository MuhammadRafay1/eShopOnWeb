namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Lifecycle of a shopper's investor sign-up, mirrored from what actually happened at Upvest.
/// </summary>
public enum InvestorStatus
{
    /// <summary>Upvest has not yet accepted the shopper and/or their account is not ready.</summary>
    Pending = 0,

    /// <summary>Upvest accepted the shopper and their managed-portfolio account is ready.</summary>
    Active = 1,

    /// <summary>Upvest will not take the shopper on.</summary>
    Rejected = 2
}

/// <summary>
/// Lifecycle of a pay-in, mirrored from its outcome at Upvest.
/// </summary>
public enum PaymentStatus
{
    /// <summary>The payment's outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>The money was credited and invested across the model's funds.</summary>
    Invested = 1,

    /// <summary>The payment failed at Upvest.</summary>
    Failed = 2
}

/// <summary>
/// Lifecycle of a realignment, mirrored from its outcome at Upvest.
/// </summary>
public enum RealignmentStatus
{
    /// <summary>The realignment's outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>The realignment completed at Upvest.</summary>
    Completed = 1,

    /// <summary>The realignment failed at Upvest.</summary>
    Failed = 2
}
