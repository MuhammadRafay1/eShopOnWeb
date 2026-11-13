namespace Microsoft.eShopWeb.PublicApi.Portfolio.Domain;

/// <summary>Where a shopper's investor sign-up has got to at Upvest.</summary>
public enum InvestorStatus
{
    /// <summary>Sign-up accepted locally; Upvest has not yet made the account ready.</summary>
    Pending = 0,

    /// <summary>Upvest has accepted the shopper and their account is ready to hold a portfolio.</summary>
    Active = 1,

    /// <summary>Upvest will not take the shopper on.</summary>
    Rejected = 2
}

/// <summary>Where a pay-in has got to at Upvest.</summary>
public enum PaymentStatus
{
    /// <summary>The outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>The money was credited and invested across the model's funds.</summary>
    Invested = 1,

    /// <summary>The pay-in failed at Upvest.</summary>
    Failed = 2
}

/// <summary>Where a realignment has got to at Upvest.</summary>
public enum RealignmentStatus
{
    /// <summary>The outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>The realignment completed at Upvest.</summary>
    Completed = 1,

    /// <summary>The realignment failed at Upvest.</summary>
    Failed = 2
}
