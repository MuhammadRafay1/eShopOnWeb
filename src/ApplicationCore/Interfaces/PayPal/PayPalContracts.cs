using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

/// <summary>
/// Application-level contracts exchanged with the PayPal client. These keep ApplicationCore free
/// of any HTTP/JSON specifics: the Infrastructure implementation maps them to and from the shapes
/// defined by the PayPal OpenAPI specs.
/// </summary>

/// <summary>Full card details for a one-off payment or a vault request. Never persisted or logged.</summary>
public record PayPalCard(
    string Name,
    string Number,
    string Expiry,          // YYYY-MM, per the spec's card_request.expiry
    string SecurityCode,
    PayPalBillingAddress? BillingAddress);

public record PayPalBillingAddress(
    string? AddressLine1,
    string? AddressLine2,
    string? AdminArea2,     // city
    string? AdminArea1,     // state / province
    string? PostalCode,
    string CountryCode);    // 2-letter, required by the spec

/// <summary>Result of creating an AUTHORIZE-intent order with a card or vault id.</summary>
public record PayPalAuthorizeResult(
    string PayPalOrderId,
    string OrderStatus,             // CREATED / APPROVED / COMPLETED / PAYER_ACTION_REQUIRED ...
    string AuthorizationId,
    string AuthorizationStatus,     // CREATED / DENIED / ...
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset? ExpiresAt);

/// <summary>Current state of an authorization (from GET / reauthorize).</summary>
public record PayPalAuthorizationDetails(
    string AuthorizationId,
    string Status,
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset? ExpiresAt);

/// <summary>Result of capturing an authorization at fulfilment.</summary>
public record PayPalCaptureResult(
    string CaptureId,
    string Status,
    decimal GrossAmount,
    decimal? PayPalFee,
    decimal? NetAmount,
    string CurrencyCode,
    DateTimeOffset CapturedAt);

/// <summary>Result of a refund against a capture.</summary>
public record PayPalRefundResult(
    string RefundId,
    string Status,
    decimal Amount,
    string CurrencyCode);

/// <summary>Result of vaulting a card.</summary>
public record PayPalVaultResult(
    string VaultId,
    string CustomerId,
    string Brand,
    string Last4,
    int ExpiryMonth,
    int ExpiryYear,
    string? Name);

/// <summary>A vaulted card as PayPal lists it (used to confirm existence / display).</summary>
public record PayPalVaultedCard(
    string VaultId,
    string Brand,
    string Last4,
    int ExpiryMonth,
    int ExpiryYear);

/// <summary>One PayPal transaction as reported by the transaction-search API.</summary>
public record PayPalTransaction(
    string? TransactionId,
    string? ReferenceId,
    string? ReferenceIdType,
    string? EventCode,
    string? Status,
    decimal? Amount,
    string? CurrencyCode,
    decimal? FeeAmount,
    string? InvoiceId,
    string? CustomField,
    DateTimeOffset? InitiationDate,
    DateTimeOffset? UpdatedDate);
