using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Maps the domain enrolment/investment statuses to the fixed wire strings callers expect.</summary>
internal static class InvestingStatusText
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
