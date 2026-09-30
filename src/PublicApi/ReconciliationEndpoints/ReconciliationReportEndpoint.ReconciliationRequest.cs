using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.PublicApi.ReconciliationEndpoints;

public class ReconciliationRequest : BaseRequest
{
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
}

public class ReconciliationResponse
{
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
    public int MatchedCount { get; set; }
    public int PayPalOnlyCount { get; set; }
    public int EShopOnlyCount { get; set; }
    public IReadOnlyList<ReconciliationMatch> Matched { get; set; } = new List<ReconciliationMatch>();
    public IReadOnlyList<PayPalOnlyRecord> PayPalOnly { get; set; } = new List<PayPalOnlyRecord>();
    public IReadOnlyList<EShopOnlyRecord> EShopOnly { get; set; } = new List<EShopOnlyRecord>();
}
