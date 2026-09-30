using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public record ReconciliationEntry(
    int? OrderId,
    string? PayPalTransactionId,
    string? InvoiceId,
    decimal? PayPalAmount,
    decimal? PayPalFee,
    string? PayPalStatus,
    decimal? EShopCapturedAmount,
    string? EShopPaymentStatus);

public record ReconciliationReport(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<ReconciliationEntry> Matched,
    IReadOnlyList<ReconciliationEntry> InPayPalOnly,
    IReadOnlyList<ReconciliationEntry> InEShopOnly);
