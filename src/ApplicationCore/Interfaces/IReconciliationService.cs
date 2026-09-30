using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public enum ReconciledTransactionKind
{
    Capture,
    Refund
}

/// <summary>One row PayPal and eShop agree about.</summary>
public record MatchedTransaction(
    string PayPalTransactionId,
    ReconciledTransactionKind Kind,
    int OrderId,
    decimal PayPalAmount,
    decimal EShopAmount,
    string PayPalStatus);

/// <summary>A PayPal transaction in the range that no eShop order references.</summary>
public record UnmatchedPayPalTransaction(
    string PayPalTransactionId,
    string Status,
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset InitiatedDate,
    string? InvoiceId,
    string? CustomField);

/// <summary>An eShop capture/refund in the range that PayPal's report does not (yet) list.</summary>
public record UnmatchedEShopTransaction(
    string PayPalTransactionId,
    ReconciledTransactionKind Kind,
    int OrderId,
    decimal Amount);

public record ReconciliationReport(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<MatchedTransaction> Matched,
    IReadOnlyList<UnmatchedPayPalTransaction> InPayPalNotEShop,
    IReadOnlyList<UnmatchedEShopTransaction> InEShopNotPayPal);

/// <summary>
/// Lines up PayPal's own transaction report for a date range against eShop's order/payment
/// records, so a payment PayPal knows about and eShop doesn't - or the reverse - is visible.
/// </summary>
public interface IReconciliationService
{
    Task<ReconciliationReport> BuildReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}
