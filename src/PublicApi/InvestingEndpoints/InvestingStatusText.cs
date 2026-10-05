using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Maps the domain status enums to the fixed wire strings the API contract requires.</summary>
public static class InvestingStatusText
{
    public static string ToWire(EnrolmentStatus status) => status switch
    {
        EnrolmentStatus.Pending => "pending",
        EnrolmentStatus.Active => "active",
        EnrolmentStatus.Rejected => "rejected",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static string ToWire(InvestmentStatus status) => status switch
    {
        InvestmentStatus.Pending => "pending",
        InvestmentStatus.Settled => "settled",
        InvestmentStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
