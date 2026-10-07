using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Maps internal statuses to the fixed wire strings the API contract requires.</summary>
internal static class InvestingStatusText
{
    public static string Wire(EnrolmentStatus status) => status switch
    {
        EnrolmentStatus.Active => "active",
        EnrolmentStatus.Rejected => "rejected",
        _ => "pending",
    };

    public static string Wire(InvestmentStatus status) => status switch
    {
        InvestmentStatus.Settled => "settled",
        InvestmentStatus.Failed => "failed",
        _ => "pending",
    };
}
