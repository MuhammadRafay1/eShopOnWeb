using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Abstraction over the PayPal REST APIs used by this integration (Orders v2,
/// Payments v2, Vault v3, Transaction Search v1). Lives in ApplicationCore so the rest
/// of the app stays free of HTTP/PayPal-specific types; the implementation lives in
/// Infrastructure. All money is passed as decimals in the app and formatted by the
/// implementation. Any non-2xx PayPal response surfaces as a <c>PayPalApiException</c>.
/// </summary>
public interface IPayPalClient
{
    /// <summary>Create an order with intent=AUTHORIZE and a card / vaulted-card payment source, authorizing it synchronously (no browser step).</summary>
    Task<PayPalAuthorizationResult> AuthorizeOrderAsync(PayPalAuthorizeOrderRequest request, CancellationToken ct);

    /// <summary>Read the live status/expiry of an authorization.</summary>
    Task<PayPalAuthorizationDetails> GetAuthorizationAsync(string authorizationId, CancellationToken ct);

    /// <summary>Reauthorize a stale hold (valid from day 4 to day 29 of the original authorization).</summary>
    Task<PayPalAuthorizationResult> ReauthorizeAsync(string authorizationId, decimal amount, string currency, string requestId, CancellationToken ct);

    /// <summary>Void (release) an authorization that has not been captured.</summary>
    Task VoidAuthorizationAsync(string authorizationId, CancellationToken ct);

    /// <summary>Capture an authorized payment — this is when the money is actually taken.</summary>
    Task<PayPalCaptureResult> CaptureAsync(string authorizationId, decimal amount, string currency, string invoiceId, string requestId, CancellationToken ct);

    /// <summary>Refund a captured payment, in full (null amount) or in part.</summary>
    Task<PayPalRefundResult> RefundAsync(string captureId, decimal? amount, string? currency, string requestId, CancellationToken ct);

    /// <summary>Vault a card (create setup token then payment token) and return the durable token + safe descriptors.</summary>
    Task<PayPalVaultToken> CreateVaultedCardAsync(PayPalCardDetails card, string customerId, CancellationToken ct);

    /// <summary>List a customer's vaulted cards.</summary>
    Task<IReadOnlyList<PayPalVaultToken>> ListVaultedCardsAsync(string customerId, CancellationToken ct);

    /// <summary>Remove a vaulted card from PayPal's vault.</summary>
    Task DeleteVaultedCardAsync(string vaultTokenId, CancellationToken ct);

    /// <summary>Fetch one page of transaction-search results for a (≤31-day) window.</summary>
    Task<PayPalTransactionSearchPage> SearchTransactionsAsync(DateTimeOffset start, DateTimeOffset end, int page, int pageSize, CancellationToken ct);
}

// ---- Request / result DTOs (plain classes, no HTTP concerns) ----

/// <summary>
/// Raw card details for a one-off payment or for vaulting. Plain class (not a record)
/// with a redacting <see cref="ToString"/> so the PAN/CVV can never leak into a log line.
/// Instances are short-lived: they exist only for the single call that needs them.
/// </summary>
public sealed class PayPalCardDetails
{
    public string Number { get; init; } = string.Empty;

    /// <summary>Expiry in PayPal's "YYYY-MM" format.</summary>
    public string Expiry { get; init; } = string.Empty;

    public string? SecurityCode { get; init; }
    public string? CardholderName { get; init; }
    public PayPalBillingAddress? BillingAddress { get; init; }

    public override string ToString() => "PayPalCardDetails { number=****REDACTED****, cvv=**** }";
}

public sealed class PayPalBillingAddress
{
    public string? AddressLine1 { get; init; }
    public string? AddressLine2 { get; init; }
    public string? AdminArea2 { get; init; }   // city
    public string? AdminArea1 { get; init; }   // state
    public string? PostalCode { get; init; }
    public string CountryCode { get; init; } = "US";
}

public sealed class PayPalAuthorizeOrderRequest
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "USD";
    public string InvoiceId { get; init; } = string.Empty;
    public string CustomId { get; init; } = string.Empty;

    /// <summary>Value used as the PayPal-Request-Id header (idempotency).</summary>
    public string RequestId { get; init; } = string.Empty;

    /// <summary>One-off card. Mutually exclusive with <see cref="VaultId"/>.</summary>
    public PayPalCardDetails? Card { get; init; }

    /// <summary>A saved-card vault id. Mutually exclusive with <see cref="Card"/>.</summary>
    public string? VaultId { get; init; }
}

public sealed class PayPalAuthorizationResult
{
    public string PayPalOrderId { get; init; } = string.Empty;
    public string AuthorizationId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed class PayPalAuthorizationDetails
{
    public string Id { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed class PayPalCaptureResult
{
    public string CaptureId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal GrossAmount { get; init; }
    public decimal? PayPalFee { get; init; }
    public decimal? NetAmount { get; init; }
}

public sealed class PayPalRefundResult
{
    public string RefundId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal? TotalRefunded { get; init; }
}

public sealed class PayPalVaultToken
{
    public string Id { get; init; } = string.Empty;
    public string? Brand { get; init; }
    public string? LastDigits { get; init; }
    public string? Expiry { get; init; }
}

public sealed class PayPalTransaction
{
    public string TransactionId { get; init; } = string.Empty;
    public string? InvoiceId { get; init; }
    public string? CustomField { get; init; }
    public decimal Amount { get; init; }
    public string? Currency { get; init; }
    public decimal? FeeAmount { get; init; }
    public string? Status { get; init; }
    public string? EventCode { get; init; }
    public DateTimeOffset? InitiationDate { get; init; }
}

public sealed class PayPalTransactionSearchPage
{
    public IReadOnlyList<PayPalTransaction> Transactions { get; init; } = Array.Empty<PayPalTransaction>();
    public int Page { get; init; }
    public int? TotalPages { get; init; }
}
