using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

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

    /// <summary>PayPal and eShop agree about these.</summary>
    public List<MatchedTransaction> Matched { get; set; } = new();

    /// <summary>PayPal has a transaction in range that no eShop order references.</summary>
    public List<UnmatchedPayPalTransaction> InPayPalNotEShop { get; set; } = new();

    /// <summary>
    /// eShop captured/refunded something in range that PayPal's report doesn't (yet) list.
    /// Because of PayPal's reporting lag (up to ~3 hours), this can legitimately be non-empty
    /// for very recent activity - that is expected, not a discrepancy to act on.
    /// </summary>
    public List<UnmatchedEShopTransaction> InEShopNotPayPal { get; set; } = new();
}
