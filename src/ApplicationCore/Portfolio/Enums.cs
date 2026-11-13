namespace Microsoft.eShopWeb.ApplicationCore.Portfolio;

/// <summary>Where a shopper's investor sign-up has got to at Upvest.</summary>
public enum InvestorSignupStatus
{
    /// <summary>Submitted; Upvest has not yet accepted the shopper / their account is not yet ready.</summary>
    Pending,

    /// <summary>Upvest has accepted the shopper and their account is ready to hold a portfolio.</summary>
    Active,

    /// <summary>Upvest will not take the shopper on.</summary>
    Rejected
}

/// <summary>Where a pay-in has got to at Upvest.</summary>
public enum PaymentStatus
{
    Pending,
    Invested,
    Failed
}

/// <summary>Where a realignment has got to at Upvest.</summary>
public enum RealignmentStatus
{
    Pending,
    Completed,
    Failed
}
