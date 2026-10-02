using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>Raw card details for a one-off payment or a card to vault. Never persisted or logged.</summary>
public record CardInput(
    string Number,
    string Expiry,            // ISO-8601 YYYY-MM
    string SecurityCode,
    string? CardholderName,
    BillingAddressInput? BillingAddress);

/// <summary>A card billing address, mapped onto PayPal's portable address shape.</summary>
public record BillingAddressInput(
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? CountryCode);     // two-letter ISO country code

/// <summary>Inputs for authorizing (holding) an order total. Carries either a one-off card or a saved-card vault id.</summary>
public record AuthorizeGatewayRequest
{
    public required int OrderId { get; init; }
    public required string InvoiceId { get; init; }
    public required decimal Amount { get; init; }
    public string? Description { get; init; }

    /// <summary>A one-off card to pay with. Mutually exclusive with <see cref="SavedCardVaultId"/>.</summary>
    public CardInput? Card { get; init; }

    /// <summary>A saved card's PayPal vault id to pay with. Mutually exclusive with <see cref="Card"/>.</summary>
    public string? SavedCardVaultId { get; init; }

    public required string CreateIdempotencyKey { get; init; }
    public required string AuthorizeIdempotencyKey { get; init; }
}

/// <summary>The hold placed with PayPal.</summary>
public record AuthorizationResult(
    string PayPalOrderId,
    string AuthorizationId,
    string? Status,
    DateTimeOffset? ExpiresAt,
    decimal AuthorizedAmount);

/// <summary>A point-in-time read of an authorization's status and expiry.</summary>
public record AuthorizationSnapshot(
    string AuthorizationId,
    string? Status,
    DateTimeOffset? ExpiresAt);

/// <summary>The result of capturing an authorized payment, with what PayPal reported.</summary>
public record CaptureResult(
    string CaptureId,
    string? Status,
    decimal GrossAmount,
    decimal? PayPalFee,
    decimal? NetAmount,
    string CurrencyCode);

/// <summary>The result of refunding a captured payment.</summary>
public record RefundResult(
    string RefundId,
    string? Status,
    decimal Amount);

/// <summary>Inputs for vaulting a card for a shopper.</summary>
public record VaultCardGatewayRequest
{
    public required CardInput Card { get; init; }

    /// <summary>The shopper's existing PayPal customer id, when they already have saved cards; null to let PayPal create one.</summary>
    public string? ExistingPayPalCustomerId { get; init; }

    /// <summary>Our own buyer reference, associated with the PayPal customer.</summary>
    public required string MerchantCustomerId { get; init; }

    public required string IdempotencyKey { get; init; }
}

/// <summary>A vaulted card: PayPal's ids plus a safe description.</summary>
public record VaultedCardResult(
    string VaultId,
    string PayPalCustomerId,
    string? Brand,
    string? LastFourDigits,
    string? Expiry,
    string? CardholderName);

/// <summary>
/// The result of walking PayPal's transaction report over a range: the transactions found, plus
/// whether every page of every sub-window was walked (false if a safety cap truncated the walk).
/// </summary>
public record TransactionSearchResult(IReadOnlyList<ReconciliationTransaction> Transactions, bool Complete);

/// <summary>One PayPal transaction from the reporting API, projected to what reconciliation needs.</summary>
public record ReconciliationTransaction(
    string? TransactionId,
    string? Status,
    decimal? Amount,
    string? CurrencyCode,
    decimal? Fee,
    string? InvoiceId,
    string? CustomField,
    string? EventCode,
    DateTimeOffset? InitiationDate);
