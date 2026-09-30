using System;

namespace Microsoft.eShopWeb.ApplicationCore.Payments;

/// <summary>
/// Raw card details supplied for a one-off payment or to be vaulted. This is a transient input —
/// it is passed to PayPal and never persisted in this application's database or written to logs.
/// </summary>
public record PaymentCard(
    string Number,
    string Expiry,       // "YYYY-MM"
    string SecurityCode,
    string Name,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string State,
    string PostalCode,
    string CountryCode); // ISO 3166-1 alpha-2, e.g. "US"

/// <summary>Result of authorizing (or reauthorizing) an order total — the hold on the money.</summary>
public record PayPalAuthorizationResult(
    string PayPalOrderId,
    string AuthorizationId,
    string Status,
    DateTimeOffset? ExpiresAt);

/// <summary>Result of capturing an authorization — the money actually taken, with fee/net.</summary>
public record PayPalCaptureResult(
    string CaptureId,
    string Status,
    decimal GrossAmount,
    decimal PayPalFee,
    decimal NetAmount);

/// <summary>Result of refunding a capture, in full or in part.</summary>
public record PayPalRefundResult(
    string RefundId,
    string Status,
    decimal Amount,
    decimal TotalRefunded);

/// <summary>Result of vaulting a card — the reusable token plus a safe description of the card.</summary>
public record PayPalVaultedCard(
    string VaultId,
    string? CustomerId,
    string Brand,
    string Last4,
    string Expiry);

/// <summary>A single transaction as reported by PayPal's Transaction Search API.</summary>
public record PayPalTransaction(
    string TransactionId,
    string? ReferenceId,
    string? ReferenceIdType,
    string? InvoiceId,
    decimal Amount,
    string Currency,
    decimal? FeeAmount,
    string? Status,
    DateTimeOffset? InitiationDate);
