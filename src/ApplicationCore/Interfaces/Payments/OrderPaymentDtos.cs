using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;

public sealed record OrderLineItemRequest(int CatalogItemId, int Quantity);

public sealed record ShipToAddressRequest(string Street, string City, string? State, string Country, string ZipCode);

public sealed record PayWithRequest(CardDetails? Card, string? SavedPaymentMethodId);

public sealed record OrderLineItemView(int CatalogItemId, string ProductName, decimal UnitPrice, int Units);

public sealed record OrderSummaryView(
    int OrderId,
    DateTimeOffset OrderDate,
    decimal Total,
    IReadOnlyList<OrderLineItemView> Items,
    string PaymentStatus,
    string? AuthorizationId,
    string? CaptureId,
    decimal? CapturedGross,
    decimal? PayPalFee,
    decimal? NetAmount,
    decimal RefundedTotal);

public sealed record RefundOutcome(string RefundId, string Status, decimal Amount);

public sealed record SavedPaymentMethodView(
    string PaymentMethodId,
    string Brand,
    string LastDigits,
    string? Expiry,
    string? CardholderName);

public sealed record ReconciliationEntry(
    string? PayPalTransactionId,
    string? PayPalStatus,
    decimal? PayPalAmount,
    string? CurrencyCode,
    int? OrderId,
    string? EshopPaymentStatus,
    string MatchState); // "Matched" | "PayPalOnly" | "EshopOnly"

public sealed record ReconciliationReport(
    IReadOnlyList<ReconciliationEntry> Entries,
    int PagesFetched,
    int TotalPages,
    bool Truncated);
