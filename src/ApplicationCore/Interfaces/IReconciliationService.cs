using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Produces the reconciliation report: PayPal's own record of transactions for a date range, lined
/// up against eShop orders, so a payment one side knows about and the other doesn't is visible.
/// </summary>
public interface IReconciliationService
{
    Task<ReconciliationReport> ReconcileAsync(DateTimeOffset from, DateTimeOffset to);
}

public class ReconciliationReport
{
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }

    /// <summary>Total PayPal transactions returned for the range (across all pages/windows).</summary>
    public int PayPalTransactionCount { get; set; }

    /// <summary>PayPal transactions that matched a local order.</summary>
    public List<ReconciliationMatch> Matched { get; set; } = new();

    /// <summary>PayPal transactions with no matching local order (anomaly).</summary>
    public List<PayPalTransactionRecord> PayPalOnly { get; set; } = new();

    /// <summary>Local fulfilled/refunded orders with no matching PayPal transaction in range (anomaly).</summary>
    public List<ReconciliationLocalOrder> EShopOnly { get; set; } = new();
}

public class ReconciliationMatch
{
    public int OrderId { get; set; }
    public string OrderStatus { get; set; } = "";
    public string? PayPalOrderId { get; set; }
    public string? PayPalTransactionId { get; set; }
    public string? TransactionStatus { get; set; }
    public decimal? PayPalAmount { get; set; }
    public string? CurrencyCode { get; set; }
}

public class ReconciliationLocalOrder
{
    public int OrderId { get; set; }
    public string OrderStatus { get; set; } = "";
    public string? PayPalOrderId { get; set; }
    public string? CaptureId { get; set; }
    public decimal? CapturedAmount { get; set; }
}
