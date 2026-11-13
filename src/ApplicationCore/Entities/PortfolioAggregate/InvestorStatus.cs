namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Sign-up status the API exposes. The lower-case wire values (<c>pending</c>/<c>active</c>/<c>rejected</c>)
/// are fixed by the task; this enum is mapped to them at the API boundary.
/// </summary>
public enum InvestorStatus
{
    Pending,
    Active,
    Rejected
}

/// <summary>Payment status the API exposes: <c>pending</c>/<c>invested</c>/<c>failed</c>.</summary>
public enum PaymentStatus
{
    Pending,
    Invested,
    Failed
}

/// <summary>Realignment status the API exposes: <c>pending</c>/<c>completed</c>/<c>failed</c>.</summary>
public enum RealignmentStatus
{
    Pending,
    Completed,
    Failed
}
