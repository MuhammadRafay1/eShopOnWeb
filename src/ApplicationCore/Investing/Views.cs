namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>Enrolment state as seen by the shopper.</summary>
public record EnrolmentView(int EnrolmentId, string Status);

/// <summary>What the shopper has set aside and what has been invested so far.</summary>
public record BalanceView(decimal PendingAmount, decimal InvestedAmount);

/// <summary>A single investment as seen by the shopper.</summary>
public record InvestmentView(int InvestmentId, decimal Amount, string Status);

/// <summary>
/// Maps the internal enums to the fixed wire strings the API contract requires.
/// </summary>
public static class InvestingStatusText
{
    public static string ToWire(this EnrolmentStatus status) => status switch
    {
        EnrolmentStatus.Active => "active",
        EnrolmentStatus.Rejected => "rejected",
        _ => "pending"
    };

    public static string ToWire(this InvestmentStatus status) => status switch
    {
        InvestmentStatus.Settled => "settled",
        InvestmentStatus.Failed => "failed",
        _ => "pending"
    };
}
