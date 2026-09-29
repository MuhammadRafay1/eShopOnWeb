using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>A PayPal transaction lined up against a known eShop order.</summary>
public record MatchedTransaction(int OrderId, string PayPalTransactionId, decimal PayPalAmount,
    decimal LocalAmount, string PayPalStatus);

/// <summary>An eShop payment in range that PayPal's report doesn't (yet) show.</summary>
public record LocalOnlyPayment(int OrderId, decimal Amount, string PaymentStatus);

/// <summary>A PayPal transaction eShop has no record of — the more actionable direction.</summary>
public record PayPalOnlyTransaction(string PayPalTransactionId, string? CustomField, decimal Amount, string Status);

public record ReconciliationReport(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<MatchedTransaction> Matched,
    IReadOnlyList<LocalOnlyPayment> OnlyInEShop,
    IReadOnlyList<PayPalOnlyTransaction> OnlyInPayPal);

public interface IReconciliationService
{
    /// <summary>
    /// Lists PayPal's own record of transactions for a date range and lines them up against eShop
    /// orders. Covers the whole range (chunking + paging handled by the client), so a payment PayPal
    /// knows about and eShop doesn't — or the reverse — is visible.
    /// </summary>
    Task<ReconciliationReport> ReconcileAsync(DateTimeOffset from, DateTimeOffset to);
}
