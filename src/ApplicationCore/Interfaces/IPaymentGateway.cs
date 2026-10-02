using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The boundary over PayPal. Every PayPal interaction goes through here; the implementation owns the
/// SDK, the error boundary, and the idempotency keys on the wire. All amounts are in <see cref="Currency"/>.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>The ISO-4217 currency all amounts are expressed in, from configuration.</summary>
    string Currency { get; }

    /// <summary>Authorize (hold) an order total against a one-off or saved card. Does not take the money.</summary>
    Task<AuthorizationResult> AuthorizeAsync(AuthorizeGatewayRequest request, CancellationToken cancellationToken = default);

    /// <summary>Read an authorization's current status and expiry.</summary>
    Task<AuthorizationSnapshot> GetAuthorizationAsync(string authorizationId, CancellationToken cancellationToken = default);

    /// <summary>Renew a stale authorization so its funds are available to capture.</summary>
    Task<AuthorizationSnapshot> ReauthorizeAsync(string authorizationId, decimal amount, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Capture (take) a previously authorized payment.</summary>
    Task<CaptureResult> CaptureAsync(string authorizationId, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Void an authorization before fulfilment, releasing the held funds.</summary>
    Task VoidAsync(string authorizationId, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Refund a captured payment, in full (null amount) or in part.</summary>
    Task<RefundResult> RefundAsync(string captureId, decimal? amount, string idempotencyKey, string? invoiceId, CancellationToken cancellationToken = default);

    /// <summary>Vault a card for a shopper and return its id plus a safe description.</summary>
    Task<VaultedCardResult> VaultCardAsync(VaultCardGatewayRequest request, CancellationToken cancellationToken = default);

    /// <summary>Delete a vaulted card so it can no longer be used to pay.</summary>
    Task DeleteVaultedCardAsync(string vaultId, CancellationToken cancellationToken = default);

    /// <summary>
    /// List PayPal's own record of transactions over a date range, walking every page of every
    /// sub-window so the result covers the whole range (PayPal caps a single query at 31 days).
    /// </summary>
    Task<TransactionSearchResult> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}
