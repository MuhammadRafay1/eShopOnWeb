namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>Lifecycle of an investor sign-up, mirroring the outcome at Upvest.</summary>
public enum InvestorStatus
{
    Pending,
    Active,
    Rejected
}

/// <summary>Lifecycle of a pay-in, mirroring the outcome at Upvest.</summary>
public enum PaymentStatus
{
    Pending,
    Invested,
    Failed
}

/// <summary>Lifecycle of a realignment, mirroring the outcome at Upvest.</summary>
public enum RealignmentStatus
{
    Pending,
    Completed,
    Failed
}
