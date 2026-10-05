using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.PublicApi.Investing;

/// <summary>Maps the domain enums to the fixed lower-case strings the API contract requires.</summary>
public static class InvestingStatusText
{
    public static string ToText(this EnrolmentStatus status) => status switch
    {
        EnrolmentStatus.Active => "active",
        EnrolmentStatus.Rejected => "rejected",
        _ => "pending"
    };

    public static string ToText(this InvestmentStatus status) => status switch
    {
        InvestmentStatus.Settled => "settled",
        InvestmentStatus.Failed => "failed",
        _ => "pending"
    };
}
