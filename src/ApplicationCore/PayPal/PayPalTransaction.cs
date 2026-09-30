using System;

namespace Microsoft.eShopWeb.ApplicationCore.PayPal;

/// <summary>
/// One line of PayPal's own transaction reporting (Transaction Search v1), used to reconcile against
/// eShop's own order/payment records.
/// </summary>
public record PayPalTransaction(
    string TransactionId,
    string Status,
    decimal Amount,
    string Currency,
    decimal FeeAmount,
    string? InvoiceId,
    string? CustomField,
    DateTimeOffset TransactionDate);
