using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// A one-off card presented to pay or to be saved to the vault. Never persisted or logged by this
/// application - it exists only long enough to be handed to <see cref="IPayPalPaymentGateway"/>.
/// </summary>
public record PayPalCardInput(
    string Number,
    string Expiry,
    string? SecurityCode,
    string? CardholderName,
    string? AddressLine1,
    string? AddressLine2,
    string? AdminArea1,
    string? AdminArea2,
    string? PostalCode,
    string? CountryCode);

/// <summary>
/// Inputs for authorizing an order total: pay with a one-off <see cref="Card"/>, or name a previously
/// vaulted card via <see cref="VaultId"/>. Exactly one should be set.
/// </summary>
public record PayPalAuthorizeOrderRequest(
    int OrderId,
    decimal Amount,
    string CurrencyCode,
    string PayPalRequestId,
    PayPalCardInput? Card,
    string? VaultId);

/// <summary>A snapshot of PayPal's own authorization (hold) state.</summary>
public record PayPalAuthorizationSnapshot(string AuthorizationId, string Status, decimal Amount, DateTimeOffset? ExpiresAt);

/// <summary>The result of creating a PayPal order and placing a hold against it.</summary>
public record PayPalOrderAuthorization(string PayPalOrderId, PayPalAuthorizationSnapshot Authorization);

/// <summary>What PayPal reports once an authorization has been captured (money actually taken).</summary>
public record PayPalCaptureResult(string CaptureId, string Status, decimal GrossAmount, decimal? FeeAmount, decimal? NetAmount);

/// <summary>The result of a refund against a capture.</summary>
public record PayPalRefundResult(string RefundId, string Status, decimal Amount);

/// <summary>The safe descriptor PayPal returns for a card it vaulted. Never carries PAN/CVV.</summary>
public record PayPalVaultedCardResult(string VaultId, string? CustomerId, string? Brand, string? LastDigits, string? Expiry, string? CardholderName);

/// <summary>Inputs for vaulting a card for later reuse by the same buyer.</summary>
public record PayPalVaultCardRequest(PayPalCardInput Card, string? ExistingCustomerId, string PayPalRequestId);

/// <summary>One row of PayPal's own transaction report.</summary>
public record PayPalTransactionRecord(
    string TransactionId,
    decimal? Amount,
    string? CurrencyCode,
    string? Status,
    string? InvoiceId,
    string? CustomField,
    DateTimeOffset? InitiationDate,
    decimal? FeeAmount);

/// <summary>
/// The result of searching PayPal's transactions over a single window (&lt;= 31 days, PayPal's own
/// search limit). <see cref="Complete"/> is false when a page cap was hit before PayPal signalled the
/// end of the result set, so the caller can tell a truncated report from a genuinely empty one.
/// </summary>
public record PayPalTransactionSearchResult(IReadOnlyList<PayPalTransactionRecord> Transactions, bool Complete);
