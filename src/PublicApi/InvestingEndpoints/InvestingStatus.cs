using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Maps the domain's enrolment and investment states to the fixed API status strings.</summary>
public static class InvestingStatus
{
    public static string ToApi(InvestorStatus status) => status switch
    {
        InvestorStatus.Active => "active",
        InvestorStatus.Rejected => "rejected",
        _ => "pending",
    };

    public static string ToApi(InvestmentStatus status) => status switch
    {
        InvestmentStatus.Settled => "settled",
        InvestmentStatus.Failed => "failed",
        _ => "pending",
    };
}
