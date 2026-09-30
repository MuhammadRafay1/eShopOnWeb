using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Models.Payments;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class ReconciliationResponse : BaseResponse
{
    public ReconciliationResponse(Guid correlationId) : base(correlationId)
    {
    }

    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
    public IReadOnlyList<ReconciliationMatch> Matched { get; set; } = Array.Empty<ReconciliationMatch>();
    public IReadOnlyList<GatewayTransaction> PayPalOnly { get; set; } = Array.Empty<GatewayTransaction>();
    public IReadOnlyList<ReconciliationEShopEntry> EShopOnly { get; set; } = Array.Empty<ReconciliationEShopEntry>();
    public string Note { get; set; } = string.Empty;
}
