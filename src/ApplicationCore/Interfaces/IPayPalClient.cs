using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The only thing in this application that talks to PayPal. Returns small DTOs (not raw JSON) so
/// callers never see PayPal's wire shape. Every money-moving call is expected to be given a stable
/// idempotency key (PayPal-Request-Id) by the caller so retries/double-clicks are safe.
/// </summary>
public interface IPayPalClient
{
    Task<AuthorizationResult> AuthorizeOrderWithCardAsync(decimal amount, string currency, string customId, string invoiceId, CardDetails card, string requestId);

    Task<AuthorizationResult> AuthorizeOrderWithVaultAsync(decimal amount, string currency, string customId, string invoiceId, string vaultId, string requestId);

    Task<CaptureResult> CaptureAsync(string authorizationId, decimal amount, string currency, string requestId);

    Task<ReauthorizeResult> ReauthorizeAsync(string authorizationId, decimal amount, string currency, string requestId);

    Task VoidAsync(string authorizationId, string requestId);

    Task<RefundResult> RefundAsync(string captureId, decimal? amount, string currency, string idempotencyKey);

    Task<AuthorizationStatusResult> GetAuthorizationAsync(string authorizationId);

    Task<VaultResult> VaultCardAsync(CardDetails card, string? customerId, string requestId);

    Task DeleteVaultTokenAsync(string vaultTokenId);

    Task<IReadOnlyList<PayPalTransaction>> SearchTransactionsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, string currency);
}
