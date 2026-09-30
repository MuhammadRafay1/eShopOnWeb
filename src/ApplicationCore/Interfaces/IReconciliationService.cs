using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public record MatchedReconciliationEntry(string InvoiceReference, int OrderId, decimal EShopAmount, decimal PayPalAmount, string PayPalTransactionStatus, bool AmountMismatch);

public record PayPalOnlyTransaction(string TransactionId, string Status, decimal Amount, string? InvoiceId, DateTimeOffset? InitiatedDate);

public record EShopOnlyPayment(int OrderId, string InvoiceReference, decimal Amount, string Status);

public record ReconciliationReport(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<MatchedReconciliationEntry> Matched,
    IReadOnlyList<PayPalOnlyTransaction> PayPalOnly,
    IReadOnlyList<EShopOnlyPayment> EShopOnly);

/// <summary>
/// Lines PayPal's own record of transactions for a date range up against local eShop payments,
/// so a transaction PayPal knows about that eShop doesn't (or vice versa) is visible. PayPal's
/// transaction reporting lags live activity by up to ~3 hours, so a range covering just-created
/// payments can legitimately come back with no PayPal-side matches - that is expected, not a bug.
/// </summary>
public interface IReconciliationService
{
    Task<ReconciliationReport> ReconcileAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}
