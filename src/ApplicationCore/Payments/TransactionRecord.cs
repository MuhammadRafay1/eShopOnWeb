using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Payments;

/// <summary>One PayPal transaction as reported by the transaction-search API, for reconciliation.</summary>
public sealed record TransactionRecord(
    string? TransactionId,
    decimal? Amount,
    string? CurrencyCode,
    string? Status,
    DateTimeOffset? InitiationDate);

/// <summary>
/// The result of searching PayPal transactions over a date range. <see cref="Truncated"/> is set when a page or
/// chunk cap was hit, so the caller learns the answer was cut short (never only via a log line).
/// </summary>
public sealed record TransactionSearchResult(
    IReadOnlyList<TransactionRecord> Records,
    bool Truncated,
    DateTimeOffset? TruncatedAfter);
