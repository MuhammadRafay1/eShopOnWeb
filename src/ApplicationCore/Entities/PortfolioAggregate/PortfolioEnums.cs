namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Where a shopper's investor sign-up has got to at Upvest.
/// </summary>
public enum InvestorSignupStatus
{
    /// <summary>Upvest has not yet accepted the shopper / their account is not yet ready.</summary>
    Pending = 0,

    /// <summary>Upvest has accepted the shopper and their account is ready to hold a portfolio.</summary>
    Active = 1,

    /// <summary>Upvest will not take the shopper on.</summary>
    Rejected = 2
}

/// <summary>
/// Where a pay-in has got to at Upvest.
/// </summary>
public enum PaymentStatus
{
    /// <summary>The payment's outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>The money has been invested across the model's funds.</summary>
    Invested = 1,

    /// <summary>The payment failed at Upvest.</summary>
    Failed = 2
}

/// <summary>
/// Where a realignment has got to at Upvest.
/// </summary>
public enum RealignmentStatus
{
    /// <summary>The realignment's outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>Every order the realignment produced has completed.</summary>
    Completed = 1,

    /// <summary>The realignment failed at Upvest.</summary>
    Failed = 2
}
