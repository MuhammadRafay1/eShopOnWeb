using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>One side of a reconciliation match: what eShop knows about the order's payment.</summary>
public record ReconciliationOrderSide(int OrderId, string? PayPalOrderId, string? CaptureId, decimal? CapturedAmount, string? Status);

/// <summary>One side of a reconciliation match: what PayPal's own transaction report says.</summary>
public record ReconciliationPayPalSide(string TransactionId, decimal? Amount, string? Status, string? InvoiceId, string? CustomField);

public record ReconciliationMatch(ReconciliationOrderSide Order, ReconciliationPayPalSide PayPal);

/// <summary>
/// A reconciliation report over [From, To]: PayPal's own record of transactions lined up against
/// eShop's OrderPayments. Matched, PayPal-only (PayPal knows about it, eShop doesn't) and eShop-only
/// (the reverse) are reported separately. <see cref="Complete"/> is false if a safety cap was hit before
/// PayPal signalled the end of its result set for any window in range.
/// </summary>
public record ReconciliationReport(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<ReconciliationMatch> Matched,
    IReadOnlyList<ReconciliationPayPalSide> PayPalOnly,
    IReadOnlyList<ReconciliationOrderSide> EShopOnly,
    bool Complete);

public interface IReconciliationService
{
    Task<ReconciliationReport> BuildReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
