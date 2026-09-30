using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.PayPal;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The PayPal operations the domain needs, expressed in domain terms rather than PayPal's wire format.
/// Implemented by an HTTP adapter in Infrastructure. Throws <see cref="Exceptions.PayPalException"/> (or
/// a subclass) on any failure PayPal reports back.
/// </summary>
public interface IPayPalGateway
{
    Task<PayPalAuthorizationResult> AuthorizeAsync(PayPalAuthorizeRequest request, CancellationToken ct = default);

    /// <summary>Captures the full amount of an authorization. Throws <see cref="Exceptions.PayPalAuthorizationStaleException"/> if the authorization is no longer capturable and might still be renewable.</summary>
    Task<PayPalCaptureResult> CaptureAsync(string authorizationId, string idempotencyKey, CancellationToken ct = default);

    Task<PayPalAuthorizationResult> ReauthorizeAsync(string authorizationId, decimal amount, string currency, string idempotencyKey, CancellationToken ct = default);

    Task VoidAsync(string authorizationId, string idempotencyKey, CancellationToken ct = default);

    /// <summary>Refunds a capture. <paramref name="amount"/> null means a full refund of whatever remains.</summary>
    Task<PayPalRefundResult> RefundAsync(string captureId, decimal? amount, string currency, string idempotencyKey, CancellationToken ct = default);

    Task<PayPalVaultResult> VaultCardAsync(PayPalCardDetails card, CancellationToken ct = default);

    Task DeleteVaultedCardAsync(string vaultId, CancellationToken ct = default);

    /// <summary>PayPal's own transaction reporting for the given range, across as many pages/windows as needed to cover it fully.</summary>
    Task<IReadOnlyList<PayPalTransaction>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}
