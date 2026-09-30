using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Payments;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

// Implemented in Infrastructure (the only project allowed to reference the PayPal SDK).
// Returns only ApplicationCore-owned DTOs — never PayPal SDK types.
public interface IPaymentGateway
{
    Task<GatewayCreatedOrder> CreateAuthorizeOrderAsync(decimal amount, string currency, string invoiceReference, string idempotencyKey, CancellationToken ct);

    Task<GatewayAuthorization> AuthorizeWithCardAsync(string payPalOrderId, CardInput card, string idempotencyKey, CancellationToken ct);

    Task<GatewayAuthorization> AuthorizeWithVaultAsync(string payPalOrderId, string vaultId, string idempotencyKey, CancellationToken ct);

    Task<GatewayCapture> CaptureAsync(string authorizationId, string idempotencyKey, CancellationToken ct);

    Task<GatewayAuthorization> ReauthorizeAsync(string authorizationId, decimal amount, string currency, string idempotencyKey, CancellationToken ct);

    Task VoidAsync(string authorizationId, CancellationToken ct);

    Task<GatewayRefund> RefundAsync(string captureId, decimal? amount, string currency, string idempotencyKey, CancellationToken ct);

    Task<GatewaySavedCard> CreateVaultCardAsync(CardInput card, string merchantCustomerId, string idempotencyKey, CancellationToken ct);

    Task DeleteVaultCardAsync(string vaultId, CancellationToken ct);

    Task<IReadOnlyList<GatewayTransaction>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
