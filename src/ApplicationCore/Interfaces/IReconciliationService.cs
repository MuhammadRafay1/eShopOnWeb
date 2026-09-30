using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Builds a reconciliation report lining PayPal's own transaction records up against local eShop payments
/// for a date range, surfacing anything PayPal knows about that eShop doesn't (and the reverse).
/// </summary>
public interface IReconciliationService
{
    Task<ReconciliationReport> BuildReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}

/// <summary>A PayPal transaction paired with the local eShop order it corresponds to.</summary>
public record ReconciliationMatch(
    string PayPalTransactionId,
    decimal? PayPalAmount,
    string? PayPalStatus,
    int OrderId,
    string LocalPayPalReference,
    string LocalReferenceKind);

/// <summary>A PayPal transaction with no matching local record.</summary>
public record PayPalOnlyRecord(
    string PayPalTransactionId,
    decimal? Amount,
    string? CurrencyCode,
    string? Status,
    DateTimeOffset? InitiatedAt,
    string? InvoiceId);

/// <summary>A local eShop payment reference PayPal did not report in this range.</summary>
public record LocalOnlyRecord(
    int OrderId,
    string PayPalReference,
    string ReferenceKind,
    decimal? Amount);

public record ReconciliationSummary(
    int PayPalTransactionCount,
    int LocalReferenceCount,
    int MatchedCount,
    int PayPalOnlyCount,
    int LocalOnlyCount);

public record ReconciliationReport(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<ReconciliationMatch> Matched,
    IReadOnlyList<PayPalOnlyRecord> PayPalOnly,
    IReadOnlyList<LocalOnlyRecord> LocalOnly,
    ReconciliationSummary Summary);
