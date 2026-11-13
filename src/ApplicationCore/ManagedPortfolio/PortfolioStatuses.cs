namespace Microsoft.eShopWeb.ApplicationCore.ManagedPortfolio;

/// <summary>
/// Lifecycle of an investor sign-up, as it is reported to API callers. The wire form is the
/// lower-cased name. <see cref="Pending"/> until Upvest has accepted the shopper and the account that can
/// hold a managed portfolio is ready; <see cref="Active"/> once it is; <see cref="Rejected"/> if Upvest
/// will not take the shopper on.
/// </summary>
public enum InvestorStatus
{
    Pending,
    Active,
    Rejected
}

/// <summary>
/// Lifecycle of a pay-in. <see cref="Pending"/> until the outcome at Upvest is known, then
/// <see cref="Invested"/> or <see cref="Failed"/>.
/// </summary>
public enum PaymentStatus
{
    Pending,
    Invested,
    Failed
}

/// <summary>
/// Lifecycle of a realignment. <see cref="Pending"/> until the outcome at Upvest is known, then
/// <see cref="Completed"/> or <see cref="Failed"/>.
/// </summary>
public enum RealignmentStatus
{
    Pending,
    Completed,
    Failed
}

/// <summary>
/// Renders the fixed, lower-cased wire values the API contract requires for each status.
/// </summary>
public static class PortfolioStatusWire
{
    public static string ToWire(this InvestorStatus status) => status switch
    {
        InvestorStatus.Active => "active",
        InvestorStatus.Rejected => "rejected",
        _ => "pending"
    };

    public static string ToWire(this PaymentStatus status) => status switch
    {
        PaymentStatus.Invested => "invested",
        PaymentStatus.Failed => "failed",
        _ => "pending"
    };

    public static string ToWire(this RealignmentStatus status) => status switch
    {
        RealignmentStatus.Completed => "completed",
        RealignmentStatus.Failed => "failed",
        _ => "pending"
    };
}
