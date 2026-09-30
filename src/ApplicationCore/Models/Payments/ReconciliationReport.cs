using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Models.Payments;

/// <summary>One PayPal transaction lined up against the eShop payment it corresponds to.</summary>
public class ReconciliationMatch
{
    public int OrderId { get; set; }
    public string CorrelationReference { get; set; } = string.Empty;
    public decimal EShopAmount { get; set; }
    public string EShopStatus { get; set; } = string.Empty;
    public string PayPalTransactionId { get; set; } = string.Empty;
    public decimal? PayPalAmount { get; set; }
    public string? PayPalStatus { get; set; }
}

/// <summary>An eShop payment with no matching PayPal transaction in range.</summary>
public class ReconciliationEShopEntry
{
    public int OrderId { get; set; }
    public string CorrelationReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>PayPal's transactions for [From, To] lined up against eShop's own payment records.</summary>
public class ReconciliationReport
{
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
    public IReadOnlyList<ReconciliationMatch> Matched { get; set; } = Array.Empty<ReconciliationMatch>();
    public IReadOnlyList<GatewayTransaction> PayPalOnly { get; set; } = Array.Empty<GatewayTransaction>();
    public IReadOnlyList<ReconciliationEShopEntry> EShopOnly { get; set; } = Array.Empty<ReconciliationEShopEntry>();
    public string Note { get; set; } = string.Empty;
}
