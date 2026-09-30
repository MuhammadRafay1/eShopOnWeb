using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Payments;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Thin abstraction over the PayPal REST API used by this integration. Implementations perform the
/// HTTP calls, manage the OAuth token, attach idempotency headers, and translate PayPal error
/// envelopes into the typed exceptions in ApplicationCore.Exceptions. All money-mutating calls take
/// a caller-derived <c>requestId</c> that becomes the PayPal-Request-Id idempotency header.
/// </summary>
public interface IPayPalGateway
{
    /// <summary>Authorize (hold) an order total using raw card details.</summary>
    Task<PayPalAuthorizationResult> AuthorizeWithCardAsync(decimal amount, string currency,
        string invoiceId, PaymentCard card, string requestId, CancellationToken cancellationToken = default);

    /// <summary>Authorize (hold) an order total using a previously-vaulted card.</summary>
    Task<PayPalAuthorizationResult> AuthorizeWithVaultAsync(decimal amount, string currency,
        string invoiceId, string vaultId, string requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Capture (take) an authorized amount. Throws <see cref="Exceptions.PayPalAuthorizationExpiredException"/>
    /// when PayPal reports the authorization has expired, so the caller can reauthorize and retry.
    /// </summary>
    Task<PayPalCaptureResult> CaptureAsync(string authorizationId, decimal amount, string currency,
        string invoiceId, string requestId, CancellationToken cancellationToken = default);

    /// <summary>Renew a stale authorization, opening a new honor period on a new authorization id.</summary>
    Task<PayPalAuthorizationResult> ReauthorizeAsync(string authorizationId, decimal amount,
        string currency, string requestId, CancellationToken cancellationToken = default);

    /// <summary>Void (release) an authorization that has not been captured.</summary>
    Task VoidAsync(string authorizationId, string requestId, CancellationToken cancellationToken = default);

    /// <summary>Refund a capture, in full (amount null) or in part.</summary>
    Task<PayPalRefundResult> RefundAsync(string captureId, decimal? amount, string currency,
        string invoiceId, string requestId, CancellationToken cancellationToken = default);

    /// <summary>Vault a card for later reuse (save without purchase), returning the reusable token.</summary>
    Task<PayPalVaultedCard> VaultCardAsync(PaymentCard card, string requestId,
        CancellationToken cancellationToken = default);

    /// <summary>Remove a card from PayPal's vault.</summary>
    Task DeleteVaultedCardAsync(string vaultId, CancellationToken cancellationToken = default);

    /// <summary>
    /// List PayPal's own record of transactions across a date range, chunking into ≤31-day windows
    /// and paging each window fully so the whole range is covered.
    /// </summary>
    Task<IReadOnlyList<PayPalTransaction>> SearchTransactionsAsync(DateTimeOffset from,
        DateTimeOffset to, CancellationToken cancellationToken = default);
}
