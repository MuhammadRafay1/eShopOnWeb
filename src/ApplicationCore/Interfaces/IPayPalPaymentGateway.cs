using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PaymentGateway;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// PayPal-agnostic boundary over the payment provider. Signatures use only primitives and ApplicationCore
/// DTOs — no SDK type crosses this interface — so the domain/orchestration code is unit-testable by faking
/// this one seam and the SDK dependency stays confined to Infrastructure.
/// </summary>
public interface IPayPalPaymentGateway
{
    Task<AuthorizationResult> AuthorizeWithCardAsync(
        string idempotencyKey, int orderId, decimal amount, string currency, CardDetails card, CancellationToken ct);

    Task<AuthorizationResult> AuthorizeWithVaultedCardAsync(
        string idempotencyKey, int orderId, decimal amount, string currency, string vaultId, CancellationToken ct);

    Task<AuthorizationResult> ReauthorizeAsync(
        string idempotencyKey, string authorizationId, decimal amount, string currency, CancellationToken ct);

    Task<AuthorizationResult> GetAuthorizationAsync(string authorizationId, CancellationToken ct);

    Task VoidAuthorizationAsync(string idempotencyKey, string authorizationId, CancellationToken ct);

    Task<CaptureResult> CaptureAuthorizationAsync(string idempotencyKey, string authorizationId, CancellationToken ct);

    Task<CaptureResult> GetCaptureAsync(string captureId, CancellationToken ct);

    Task<RefundResult> RefundCaptureAsync(
        string idempotencyKey, string captureId, decimal? amount, string currency, CancellationToken ct);

    Task<RefundResult> GetRefundAsync(string refundId, CancellationToken ct);

    Task<VaultedCardResult> CreateVaultedCardAsync(
        string idempotencyKey, string buyerId, CardDetails card, CancellationToken ct);

    Task DeleteVaultedCardAsync(string vaultId, CancellationToken ct);

    Task<IReadOnlyList<PayPalTransactionRecord>> SearchTransactionsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
