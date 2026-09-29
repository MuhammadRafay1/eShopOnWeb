using System;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

/// <summary>
/// Domain-facing contracts for talking to PayPal. Deliberately free of any PayPal wire/JSON
/// detail so ApplicationCore services depend only on this abstraction (mirroring the
/// IRepository/EfRepository split); the Infrastructure implementation maps these to and from the
/// documented PayPal request/response bodies.
/// </summary>

/// <summary>Raw card data for a one-off payment or a vault save. Held only for the duration of the
/// outbound PayPal call — never persisted, never logged, never echoed back.</summary>
public record CardDetails(
    string Name,
    string Number,
    string Expiry,          // "YYYY-MM"
    string SecurityCode,
    CardBillingAddress? BillingAddress);

public record CardBillingAddress(
    string? AddressLine1,
    string? AddressLine2,
    string? AdminArea2,     // city / town
    string? AdminArea1,     // state / province
    string? PostalCode,
    string? CountryCode);   // 2-char ISO 3166-1

/// <summary>The result of authorizing an order (single-step card create, or vault_id pay).</summary>
public record AuthorizationResult(
    string PayPalOrderId,
    string AuthorizationId,
    string Status,
    DateTimeOffset ExpiresAt);

/// <summary>The current server-side state of an authorization (from a GET before capture).</summary>
public record AuthorizationDetails(
    string AuthorizationId,
    string Status,
    DateTimeOffset? ExpiresAt,
    string? StatusReason);

public record ReauthorizationResult(
    string AuthorizationId,
    string Status,
    DateTimeOffset ExpiresAt);

public record CaptureResult(
    string CaptureId,
    string Status,
    decimal Amount,
    decimal? PayPalFee,
    decimal? NetAmount);

public record RefundResult(
    string RefundId,
    string Status,
    decimal Amount);

/// <summary>The vaulted card: durable token id plus display-safe descriptors.</summary>
public record VaultedCard(
    string VaultId,
    string Brand,
    string Last4Digits,
    string Expiry);

public record SetupTokenResult(
    string SetupTokenId,
    string Status);

/// <summary>One transaction as reported by PayPal Transaction Search.</summary>
public record PayPalTransaction(
    string TransactionId,
    string? CustomField,
    decimal Amount,
    string CurrencyCode,
    string Status,
    string? EventCode,
    DateTimeOffset? InitiationDate);
