namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// The lifecycle status of a shopper's investor sign-up, as reflected by Upvest.
/// </summary>
public enum InvestorStatus
{
    /// <summary>Upvest has not yet accepted the shopper and/or their account is not yet ready.</summary>
    Pending = 0,

    /// <summary>Upvest has accepted the shopper and their portfolio account is ready.</summary>
    Active = 1,

    /// <summary>Upvest will not take the shopper on.</summary>
    Rejected = 2
}

/// <summary>
/// The status of a pay-in, as reflected by Upvest.
/// </summary>
public enum PaymentStatus
{
    /// <summary>The payment's outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>The payment has been invested across the model's funds.</summary>
    Invested = 1,

    /// <summary>The payment failed at Upvest.</summary>
    Failed = 2
}

/// <summary>
/// The status of a realignment, as reflected by Upvest.
/// </summary>
public enum RealignmentStatus
{
    /// <summary>The realignment's outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>Every rebalancing order completed.</summary>
    Completed = 1,

    /// <summary>The realignment failed at Upvest.</summary>
    Failed = 2
}
