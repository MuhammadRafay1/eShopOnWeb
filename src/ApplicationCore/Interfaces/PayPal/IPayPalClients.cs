using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

/// <summary>
/// Checkout Orders v2: create an AUTHORIZE-intent order that places a hold on the money.
/// For a direct card payment (or a saved card via vault id) the payment source is supplied
/// straight into the create-order call and PayPal processes it synchronously.
/// </summary>
public interface IPayPalOrdersClient
{
    Task<PayPalAuthorizeResult> CreateAuthorizedOrderAsync(
        decimal amount, string eShopOrderId,
        PayPalCard? card, string? vaultId,
        string requestId, CancellationToken cancellationToken = default);
}

/// <summary>Payments v2: everything after authorization — capture, reauthorize, void, refund.</summary>
public interface IPayPalPaymentsClient
{
    Task<PayPalAuthorizationDetails> GetAuthorizationAsync(
        string authorizationId, CancellationToken cancellationToken = default);

    Task<PayPalCaptureResult> CaptureAuthorizationAsync(
        string authorizationId, decimal amount, string requestId,
        CancellationToken cancellationToken = default);

    Task<PayPalAuthorizationDetails> ReauthorizeAsync(
        string authorizationId, decimal amount, string requestId,
        CancellationToken cancellationToken = default);

    Task VoidAuthorizationAsync(
        string authorizationId, string requestId, CancellationToken cancellationToken = default);

    Task<PayPalRefundResult> RefundCaptureAsync(
        string captureId, decimal? amount, string eShopOrderId, string idempotencyKey,
        CancellationToken cancellationToken = default);
}

/// <summary>Vault v3: save, list and delete cards.</summary>
public interface IPayPalVaultClient
{
    Task<PayPalVaultResult> VaultCardAsync(
        PayPalCard card, string merchantCustomerId, string? existingCustomerId,
        string requestId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PayPalVaultedCard>> ListVaultedCardsAsync(
        string customerId, CancellationToken cancellationToken = default);

    Task DeleteVaultedCardAsync(
        string vaultId, CancellationToken cancellationToken = default);
}

/// <summary>Transaction Search v1: PayPal's own record of transactions for reconciliation.</summary>
public interface IPayPalReportingClient
{
    /// <summary>
    /// Returns every PayPal transaction across the whole [from, to] range, transparently
    /// splitting it into the ≤31-day windows the API mandates and paging each window fully.
    /// </summary>
    Task<IReadOnlyList<PayPalTransaction>> SearchTransactionsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}
