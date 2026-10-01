using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Payments;

/// <summary>
/// Reconciliation of PayPal's own transaction record against eShop's payment/refund rows over a date range.
/// Rows present on only one side are surfaced as mismatches, in both directions.
/// </summary>
public sealed record ReconciliationReport(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<ReconciliationMatch> Matched,
    IReadOnlyList<ReconciliationPayPalOnly> InPayPalNotInEShop,
    IReadOnlyList<ReconciliationEShopOnly> InEShopNotInPayPal,
    bool Truncated,
    DateTimeOffset? TruncatedAfter);

/// <summary>A PayPal transaction that lines up with an eShop capture or refund.</summary>
public sealed record ReconciliationMatch(
    string TransactionId,
    string Kind,
    decimal? PayPalAmount,
    decimal? EShopAmount,
    string? PayPalStatus,
    int? OrderId);

/// <summary>A PayPal transaction with no matching eShop record.</summary>
public sealed record ReconciliationPayPalOnly(
    string? TransactionId,
    decimal? Amount,
    string? CurrencyCode,
    string? Status,
    DateTimeOffset? InitiationDate);

/// <summary>An eShop capture/refund with no matching PayPal transaction in the reported range.</summary>
public sealed record ReconciliationEShopOnly(
    string TransactionId,
    string Kind,
    decimal Amount,
    int OrderId);
