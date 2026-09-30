using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Payments;

namespace Microsoft.eShopWeb.PublicApi.ReconciliationEndpoints;

public class ReconciliationResponse : BaseResponse
{
    public ReconciliationResponse(Guid correlationId) : base(correlationId)
    {
    }

    public ReconciliationResponse()
    {
    }

    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
    public int PayPalTransactionCount { get; set; }
    public List<ReconciliationMatch> Matched { get; set; } = new();
    public List<GatewayTransaction> UnmatchedInPayPal { get; set; } = new();
    public List<UnmatchedEshopPayment> UnmatchedInEshop { get; set; } = new();
}
