using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;

/// <summary>One-off card details for a payment or a vault save. Never persisted or logged by this app.</summary>
public sealed record CardDetails(
    string Number,
    string ExpiryYearMonth, // "YYYY-MM"
    string SecurityCode,
    string CardholderName,
    string Street,
    string City,
    string? State,
    string Country,
    string PostalCode);

/// <summary>Either a one-off card or a saved (vaulted) card id - exactly one must be set.</summary>
public sealed record PaymentSource(CardDetails? Card, string? VaultId);

public sealed record AuthorizeOrderCommand(
    int OrderId,
    string InvoiceId,
    decimal Amount,
    string CurrencyCode,
    PaymentSource PaymentSource);

public sealed record AuthorizeResult(
    string PayPalOrderId,
    string AuthorizationId,
    string AuthorizationStatus,
    string? AuthorizationExpiryUtc,
    string? PaymentSourceDescriptor);

public sealed record CaptureCommand(int OrderId, string AuthorizationId, string InvoiceId);

public sealed record CaptureResult(
    string CaptureId,
    string CaptureStatus,
    decimal CapturedGross,
    decimal? Fee,
    decimal? NetAmount);

/// <summary>Thrown by the gateway when a capture fails specifically because the authorization has gone stale.</summary>
public sealed class AuthorizationStaleException : Exception
{
}

public sealed record ReauthorizeCommand(string AuthorizationId, decimal Amount, string CurrencyCode);

public sealed record ReauthorizeResult(string AuthorizationId, string Status, string? ExpiryUtc);

public sealed record RefundCommand(string CaptureId, decimal? Amount, string CurrencyCode, string IdempotencyKey);

public sealed record RefundResult(string RefundId, string Status, decimal Amount);

public sealed record SaveCardCommand(CardDetails Card, string? ExistingPayPalCustomerId, string MerchantBuyerId);

public sealed record VaultCardResult(
    string VaultId,
    string PayPalCustomerId,
    string Brand,
    string LastDigits,
    string? Expiry,
    string? CardholderName);

public sealed record ReconciliationTransaction(
    string TransactionId,
    string Status,
    decimal Amount,
    string CurrencyCode,
    decimal? FeeAmount,
    string? InvoiceId,
    string? CustomField);

public sealed record ReconciliationPage(
    IReadOnlyList<ReconciliationTransaction> Transactions,
    int Page,
    int TotalPages);

/// <summary>
/// The interface ApplicationCore programs against for every PayPal interaction. The only
/// implementation (PayPalPaymentGateway, in Infrastructure) is the sole caller of the PayPal SDK -
/// no SDK type crosses this boundary.
/// </summary>
public interface IPayPalPaymentGateway
{
    /// <summary>Creates a PayPal order (intent=AUTHORIZE) and authorizes it in one flow. Puts a hold on the funds only.</summary>
    Task<AuthorizeResult> AuthorizeOrderAsync(AuthorizeOrderCommand command, CancellationToken cancellationToken);

    /// <summary>Captures a previously authorized payment. Throws <see cref="AuthorizationStaleException"/> if the authorization has expired.</summary>
    Task<CaptureResult> CaptureAsync(CaptureCommand command, CancellationToken cancellationToken);

    /// <summary>Renews (re-authorizes) a stale authorization so it can be captured.</summary>
    Task<ReauthorizeResult> ReauthorizeAsync(ReauthorizeCommand command, CancellationToken cancellationToken);

    /// <summary>Voids (releases) an authorization before it is captured.</summary>
    Task VoidAsync(string authorizationId, CancellationToken cancellationToken);

    /// <summary>Refunds a captured payment, in full (Amount null) or in part.</summary>
    Task<RefundResult> RefundAsync(RefundCommand command, CancellationToken cancellationToken);

    /// <summary>Vaults a card (direct card vaulting) against a PayPal customer, creating the customer on first save.</summary>
    Task<VaultCardResult> SaveCardAsync(SaveCardCommand command, CancellationToken cancellationToken);

    /// <summary>Removes a vaulted card.</summary>
    Task DeleteCardAsync(string vaultId, CancellationToken cancellationToken);

    /// <summary>Reads one page of PayPal's transaction history for the given date range.</summary>
    Task<ReconciliationPage> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, int page, int pageSize, CancellationToken cancellationToken);
}
