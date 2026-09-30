using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Thin client over the PayPal REST APIs this integration uses (Checkout Orders v2,
/// Payments v2, Vault Payment Tokens v3, Transaction Search v1). Every operation maps 1:1
/// to an operationId declared in api-specs/paypal - see PLAN.md section 1 for the mapping.
/// </summary>
public interface IPayPalClient
{
    Task<PayPalOrderResult> CreateOrderAsync(PayPalCreateOrderInput input, string requestId, CancellationToken cancellationToken);

    Task<PayPalAuthorizeResult> AuthorizeOrderAsync(string payPalOrderId, string requestId, CancellationToken cancellationToken);

    Task<PayPalCaptureResult> CaptureAuthorizationAsync(string authorizationId, decimal amount, string currencyCode, string requestId, CancellationToken cancellationToken);

    Task VoidAuthorizationAsync(string authorizationId, string requestId, CancellationToken cancellationToken);

    Task<PayPalAuthorizationStatusResult> ReauthorizeAsync(string authorizationId, decimal amount, string currencyCode, string requestId, CancellationToken cancellationToken);

    Task<PayPalRefundResult> RefundCaptureAsync(string captureId, decimal? amount, string currencyCode, string requestId, CancellationToken cancellationToken);

    Task<PayPalVaultTokenResult> CreatePaymentTokenAsync(PayPalCardInput card, string? payPalCustomerId, string? merchantCustomerId, string requestId, CancellationToken cancellationToken);

    Task DeletePaymentTokenAsync(string vaultTokenId, CancellationToken cancellationToken);

    /// <summary>
    /// Searches PayPal transactions across [from, to], transparently chunking into &lt;=31-day
    /// windows and paging each window to completion (the spec caps both range and page size).
    /// </summary>
    Task<IReadOnlyList<PayPalTransaction>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
