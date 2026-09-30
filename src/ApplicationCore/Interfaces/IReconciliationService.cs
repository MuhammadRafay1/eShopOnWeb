using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Builds a reconciliation report lining up PayPal's own record of transactions against eShop
/// orders for a date range, so a payment PayPal knows about and eShop doesn't — or the reverse —
/// is visible.
/// </summary>
public interface IReconciliationService
{
    Task<ReconciliationReport> BuildReportAsync(DateTimeOffset from, DateTimeOffset to,
        CancellationToken cancellationToken = default);
}

public record ReconciliationReport(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<ReconciliationMatch> Matched,
    IReadOnlyList<PayPalOnlyRecord> PayPalOnly,
    IReadOnlyList<EShopOnlyRecord> EShopOnly)
{
    public int MatchedCount => Matched.Count;
    public int PayPalOnlyCount => PayPalOnly.Count;
    public int EShopOnlyCount => EShopOnly.Count;
    public int PayPalTransactionCount => Matched.Count + PayPalOnly.Count;
}

/// <summary>A PayPal transaction matched to an eShop order/payment.</summary>
public record ReconciliationMatch(
    int OrderId,
    string? PayPalTransactionId,
    string? PayPalReferenceId,
    string? EventCode,
    string? Status,
    decimal? PayPalAmount,
    string? CurrencyCode);

/// <summary>A PayPal transaction with no matching eShop payment in range.</summary>
public record PayPalOnlyRecord(
    string? PayPalTransactionId,
    string? PayPalReferenceId,
    string? EventCode,
    string? Status,
    decimal? Amount,
    string? CurrencyCode,
    string? InvoiceId,
    string? CustomField);

/// <summary>An eShop payment with no matching PayPal transaction in range.</summary>
public record EShopOnlyRecord(
    int OrderId,
    string? PayPalOrderId,
    string? AuthorizationId,
    string? CaptureId,
    string Status,
    decimal Amount,
    string Currency);
