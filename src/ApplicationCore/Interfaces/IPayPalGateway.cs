using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The application's abstraction over PayPal. One method per capability; the Infrastructure
/// implementation wraps the PayPal Server SDK, carries the idempotency keys, and translates SDK
/// exceptions into the app's own <see cref="Exceptions.PaymentGatewayException"/> family. All amounts
/// are decimals in the payment's configured currency; the gateway handles "to the cent" formatting.
/// </summary>
public interface IPayPalGateway
{
    /// <summary>The ISO-4217 currency every payment is taken in (from <c>PayPal:Currency</c> configuration).</summary>
    string Currency { get; }

    /// <summary>Create + authorize an order in one call (holds funds, does not capture).</summary>
    Task<PayPalAuthorizationResult> AuthorizeAsync(AuthorizeCardPaymentCommand command, CancellationToken cancellationToken);

    /// <summary>Read the current state of an authorization (used to decide whether a hold is stale).</summary>
    Task<PayPalAuthorizationState> GetAuthorizationAsync(string authorizationId, CancellationToken cancellationToken);

    /// <summary>Renew a stale hold. The returned authorization id may differ from the input one.</summary>
    Task<PayPalAuthorizationState> ReauthorizeAsync(string authorizationId, decimal amount, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>
    /// Capture a held authorization for an explicit amount — this is when money is actually taken. The
    /// amount is sent explicitly (equal to the order total) rather than relying on an implicit full capture.
    /// </summary>
    Task<PayPalCaptureResult> CaptureAsync(string authorizationId, decimal amount, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Void a held authorization — releases the hold so no money moves.</summary>
    Task<PayPalVoidResult> VoidAsync(string authorizationId, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Refund a captured payment, in full (<paramref name="amount"/> null) or in part.</summary>
    Task<PayPalRefundResult> RefundAsync(string captureId, decimal? amount, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Vault a card for reuse (setup token then payment token). Returns PayPal-masked data.</summary>
    Task<PayPalSavedCardResult> SaveCardAsync(SaveCardCommand command, CancellationToken cancellationToken);

    /// <summary>Delete a vaulted card so it can no longer be used to pay.</summary>
    Task DeleteSavedCardAsync(string vaultId, CancellationToken cancellationToken);

    /// <summary>List PayPal's record of transactions for a date range (paged internally).</summary>
    Task<PayPalTransactionReport> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}

// --- Commands ---

public sealed record AuthorizeCardPaymentCommand
{
    public required decimal Amount { get; init; }
    public required string InvoiceId { get; init; }
    public required string IdempotencyKey { get; init; }
    public string? Description { get; init; }

    /// <summary>Raw one-off card details, or null when paying with a saved card.</summary>
    public CardDetails? Card { get; init; }

    /// <summary>A saved-card vault id to pay with, or null when paying with raw card details.</summary>
    public string? VaultId { get; init; }
}

public sealed record SaveCardCommand
{
    /// <summary>A stable, PayPal-pattern-safe id for this shopper (used only when minting a new customer).</summary>
    public required string MerchantCustomerId { get; init; }

    /// <summary>The shopper's existing PayPal customer id, so all their cards land under one customer.</summary>
    public string? ExistingPayPalCustomerId { get; init; }

    public required CardDetails Card { get; init; }
}

public sealed record CardDetails
{
    public required string Number { get; init; }

    /// <summary>ISO-8601 <c>YYYY-MM</c>.</summary>
    public required string Expiry { get; init; }
    public required string SecurityCode { get; init; }
    public string? Name { get; init; }
    public required CardBillingAddress BillingAddress { get; init; }
}

public sealed record CardBillingAddress
{
    public string? Line1 { get; init; }
    public string? Line2 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }

    /// <summary>2-letter ISO 3166-1 country code (PayPal requires this shape).</summary>
    public required string CountryCode { get; init; }
}

// --- Results ---

public sealed record PayPalAuthorizationResult
{
    public required string PayPalOrderId { get; init; }
    public required string OrderStatus { get; init; }
    public required string AuthorizationId { get; init; }
    public string? AuthorizationStatus { get; init; }
    public decimal? AuthorizedAmount { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed record PayPalAuthorizationState
{
    public required string AuthorizationId { get; init; }
    public string? Status { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed record PayPalCaptureResult
{
    public required string CaptureId { get; init; }
    public string? Status { get; init; }
    public required decimal GrossAmount { get; init; }
    public decimal? PayPalFee { get; init; }
    public decimal? NetAmount { get; init; }
    public required string Currency { get; init; }
}

public sealed record PayPalVoidResult
{
    public string? Status { get; init; }
}

public sealed record PayPalRefundResult
{
    public required string RefundId { get; init; }
    public string? Status { get; init; }
    public decimal? Amount { get; init; }
}

public sealed record PayPalSavedCardResult
{
    public required string VaultId { get; init; }
    public required string PayPalCustomerId { get; init; }
    public string? Brand { get; init; }
    public string? LastDigits { get; init; }
    public string? Expiry { get; init; }
}

public sealed record PayPalTransactionReport
{
    public required IReadOnlyList<PayPalTransaction> Transactions { get; init; }

    /// <summary>True when a hard page cap was hit before PayPal's own last page — the report is partial.</summary>
    public required bool Truncated { get; init; }
}

public sealed record PayPalTransaction
{
    public string? TransactionId { get; init; }
    public string? ReferenceId { get; init; }
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
    public string? Status { get; init; }
    public string? InvoiceId { get; init; }
    public DateTimeOffset? InitiatedAt { get; init; }
}
