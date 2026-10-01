using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The sole seam onto PayPal. Wraps the PayPalServerSdk client, maps domain DTOs to/from SDK models, and
/// owns the error boundary: every PayPalServerSdk exception is translated into a
/// <see cref="Exceptions.PaymentGatewayException"/> here, so no PayPalServerSdk type leaks into
/// ApplicationCore or PublicApi. Implemented in Infrastructure.
/// </summary>
public interface IPayPalPaymentGateway
{
    /// <summary>Creates a PayPal order for the given amount and places a hold (authorization) against it.</summary>
    Task<PayPalOrderAuthorization> CreateOrderAndAuthorizeAsync(PayPalAuthorizeOrderRequest request, CancellationToken cancellationToken);

    /// <summary>Renews a stale authorization so it can still be captured.</summary>
    Task<PayPalAuthorizationSnapshot> ReauthorizeAsync(string authorizationId, decimal amount, string currencyCode, string payPalRequestId, CancellationToken cancellationToken);

    /// <summary>Captures (takes) the held funds for an authorization.</summary>
    Task<PayPalCaptureResult> CaptureAsync(string authorizationId, string payPalRequestId, CancellationToken cancellationToken);

    /// <summary>Releases a hold without ever taking the money.</summary>
    Task VoidAsync(string authorizationId, string payPalRequestId, CancellationToken cancellationToken);

    /// <summary>Refunds a captured payment, in full (amount null) or in part.</summary>
    Task<PayPalRefundResult> RefundAsync(string captureId, decimal? amount, string currencyCode, string payPalRequestId, CancellationToken cancellationToken);

    /// <summary>Re-reads an authorization's current state - used to settle an unknown outcome after a transport failure.</summary>
    Task<PayPalAuthorizationSnapshot> GetAuthorizationAsync(string authorizationId, CancellationToken cancellationToken);

    /// <summary>Re-reads a capture's current state - used to settle an unknown outcome after a transport failure.</summary>
    Task<PayPalCaptureResult> GetCaptureAsync(string captureId, CancellationToken cancellationToken);

    /// <summary>Re-reads a refund's current state - used to settle an unknown outcome after a transport failure.</summary>
    Task<PayPalRefundResult> GetRefundAsync(string refundId, CancellationToken cancellationToken);

    /// <summary>Vaults a card for reuse by the same buyer on a later order.</summary>
    Task<PayPalVaultedCardResult> VaultCardAsync(PayPalVaultCardRequest request, CancellationToken cancellationToken);

    /// <summary>Removes a card from PayPal's vault.</summary>
    Task DeleteVaultedCardAsync(string payPalVaultId, CancellationToken cancellationToken);

    /// <summary>
    /// Searches PayPal's own transaction report for a single window. The caller is responsible for
    /// chunking a longer range into windows no wider than PayPal's 31-day search limit.
    /// </summary>
    Task<PayPalTransactionSearchResult> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
