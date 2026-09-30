using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Payments;

/// <summary>
/// A reconciliation of PayPal's own transaction record against local eShop payments over a date
/// range. Surfaces transactions PayPal knows about that eShop does not, and eShop payments PayPal
/// has no record of, alongside the matched pairs.
/// </summary>
public record ReconciliationReport(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<ReconciliationMatch> Matched,
    IReadOnlyList<PayPalOnlyTransaction> PayPalOnly,
    IReadOnlyList<EShopOnlyPayment> EShopOnly);

public record ReconciliationMatch(
    int OrderId,
    int PaymentId,
    string PayPalTransactionId,
    string LocalStatus,
    string? PayPalStatus,
    decimal LocalAmount,
    decimal PayPalAmount);

public record PayPalOnlyTransaction(
    string PayPalTransactionId,
    string? ReferenceId,
    string? InvoiceId,
    decimal Amount,
    string Currency,
    string? Status);

public record EShopOnlyPayment(
    int OrderId,
    int PaymentId,
    string LocalStatus,
    decimal Amount,
    string Currency);
