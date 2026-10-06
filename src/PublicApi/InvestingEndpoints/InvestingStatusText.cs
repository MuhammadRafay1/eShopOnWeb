using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Maps internal statuses to the fixed wire values used by the API.</summary>
public static class InvestingStatusText
{
    public static string ToWire(this EnrolmentStatus status) => status switch
    {
        EnrolmentStatus.Active => "active",
        EnrolmentStatus.Rejected => "rejected",
        _ => "pending",
    };

    public static string ToWire(this InvestmentStatus status) => status switch
    {
        InvestmentStatus.Settled => "settled",
        InvestmentStatus.Failed => "failed",
        _ => "pending",
    };
}
