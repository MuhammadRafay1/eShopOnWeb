using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;

/// <summary>
/// Application-facing facade over PayPal. Endpoints only ever talk to this interface - all raw
/// HTTP/JSON concerns live in the Infrastructure implementation.
/// </summary>
public interface IPayPalPaymentGateway
{
    string Currency { get; }

    /// <summary>Creates a PayPal order and authorizes it for <paramref name="request"/>.Amount (a hold, no capture).</summary>
    Task<PayPalAuthorizationResult> AuthorizeAsync(PayPalAuthorizeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Renews a stale authorization that has passed its honor period.</summary>
    Task<PayPalAuthorizationResult> ReauthorizeAsync(int orderId, string authorizationId, decimal amount, CancellationToken cancellationToken = default);

    /// <summary>Captures the full authorized amount for an order (money actually moves here).</summary>
    Task<PayPalCaptureResult> CaptureAsync(int orderId, string authorizationId, CancellationToken cancellationToken = default);

    /// <summary>Releases a hold before capture. Idempotent at the PayPal-Request-Id level.</summary>
    Task VoidAsync(int orderId, string authorizationId, CancellationToken cancellationToken = default);

    Task<PayPalRefundResult> RefundAsync(PayPalRefundRequest request, CancellationToken cancellationToken = default);

    /// <summary>Vaults a card for later reuse. Never returns raw card data.</summary>
    Task<PayPalVaultResult> SaveCardAsync(CardDetails card, string? existingCustomerId, CancellationToken cancellationToken = default);

    /// <summary>Deletes a vaulted card. A PayPal-side 404 is treated as success by the caller.</summary>
    Task DeleteVaultedCardAsync(string vaultTokenId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every PayPal transaction between <paramref name="from"/> and <paramref name="to"/>,
    /// transparently chunking into &lt;=31-day windows and paging through each one.
    /// </summary>
    Task<IReadOnlyList<PayPalTransactionRecord>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}
