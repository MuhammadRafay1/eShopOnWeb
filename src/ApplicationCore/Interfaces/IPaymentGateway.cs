using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Models.Payments;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Port over PayPal, speaking only domain DTOs — no PayPal SDK type crosses this boundary.
/// The only implementation lives in Infrastructure, behind the PayPal .NET SDK.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Creates (or resumes) a PayPal order and authorizes it, placing a hold for the exact amount.</summary>
    Task<AuthorizationResult> AuthorizeAsync(AuthorizeCardPaymentRequest request, CancellationToken ct = default);

    /// <summary>Renews a stale authorization for the same amount.</summary>
    Task<ReauthorizationResult> ReauthorizeAsync(string authorizationId, decimal amount, string currencyCode, string requestKey, CancellationToken ct = default);

    /// <summary>Captures the full authorized amount, returning PayPal's fee/net breakdown.</summary>
    Task<CaptureResult> CaptureAsync(string authorizationId, string requestKey, CancellationToken ct = default);

    /// <summary>Releases a hold; no money moves.</summary>
    Task VoidAsync(string authorizationId, string requestKey, CancellationToken ct = default);

    /// <summary>Refunds a capture in full (amount: null) or in part.</summary>
    Task<RefundResult> RefundAsync(string captureId, decimal? amount, string currencyCode, string idempotencyKey, CancellationToken ct = default);

    /// <summary>Vaults a raw card for later reuse; the response never carries a PAN.</summary>
    Task<VaultedCardResult> VaultCardAsync(CardDetails card, CancellationToken ct = default);

    /// <summary>Deletes a vaulted card; afterwards its vault id can no longer be used to pay.</summary>
    Task DeleteVaultedCardAsync(string vaultId, CancellationToken ct = default);

    /// <summary>Lists PayPal's own transactions in [from, to], covering every page.</summary>
    Task<IReadOnlyList<GatewayTransaction>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}
