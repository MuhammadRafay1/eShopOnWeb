using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

/// <summary>
/// The one seam through which the application talks to PayPal. One method per documented PayPal
/// call. Implementations resolve the base URL, obtain/refresh the OAuth token, set the mandated
/// idempotency and representation headers, and translate PayPal wire shapes to the domain-facing
/// contracts. A payer-action/challenge response surfaces as a distinct, loud exception rather
/// than being handled.
/// </summary>
public interface IPayPalClient
{
    /// <summary>Create an AUTHORIZE-intent order paying with raw card details (single-step, no redirect).</summary>
    Task<AuthorizationResult> AuthorizeWithCardAsync(int orderId, string currencyCode, decimal amount,
        CardDetails card, CancellationToken cancellationToken = default);

    /// <summary>Create an AUTHORIZE-intent order paying with a previously vaulted card.</summary>
    Task<AuthorizationResult> AuthorizeWithVaultedCardAsync(int orderId, string currencyCode, decimal amount,
        string vaultId, CancellationToken cancellationToken = default);

    /// <summary>Read the current state of an authorization (status, expiration) before capturing.</summary>
    Task<AuthorizationDetails> GetAuthorizationAsync(string authorizationId,
        CancellationToken cancellationToken = default);

    /// <summary>Renew a stale hold. Idempotency key is caller-supplied and deterministic per attempt.</summary>
    Task<ReauthorizationResult> ReauthorizeAsync(string authorizationId, string currencyCode, decimal amount,
        string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Capture (take the money for) an authorization at fulfilment.</summary>
    Task<CaptureResult> CaptureAsync(string authorizationId, string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>Void (release) an authorization before fulfilment.</summary>
    Task VoidAsync(string authorizationId, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Refund a capture, in full (amount = null) or in part.</summary>
    Task<RefundResult> RefundAsync(string captureId, string currencyCode, decimal? amount,
        string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Vault a card: create a setup token from raw card details.</summary>
    Task<SetupTokenResult> CreateSetupTokenAsync(CardDetails card, CancellationToken cancellationToken = default);

    /// <summary>Exchange a setup token for a durable payment (vault) token.</summary>
    Task<VaultedCard> CreatePaymentTokenAsync(string setupTokenId, CancellationToken cancellationToken = default);

    /// <summary>Delete a vaulted card so it can no longer be used to pay.</summary>
    Task DeletePaymentTokenAsync(string vaultId, CancellationToken cancellationToken = default);

    /// <summary>
    /// List PayPal's own record of transactions across a date range, transparently chunking into
    /// the documented ≤31-day windows and paging through every page so the whole range is covered.
    /// </summary>
    Task<IReadOnlyList<PayPalTransaction>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to,
        CancellationToken cancellationToken = default);
}
