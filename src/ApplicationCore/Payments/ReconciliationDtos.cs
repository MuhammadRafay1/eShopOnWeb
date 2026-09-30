using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Payments;

public record ReconciliationMatch(int OrderId, string InvoiceReference, string PayPalTransactionId, decimal EshopAmount, decimal PayPalAmount, string Currency, string? PayPalStatus);

public record UnmatchedEshopPayment(int OrderId, string InvoiceReference, decimal Amount, string Currency, string PaymentStatus);

public record ReconciliationReport(
    DateTimeOffset From,
    DateTimeOffset To,
    int PayPalTransactionCount,
    IReadOnlyList<ReconciliationMatch> Matched,
    IReadOnlyList<GatewayTransaction> UnmatchedInPayPal,
    IReadOnlyList<UnmatchedEshopPayment> UnmatchedInEshop);
