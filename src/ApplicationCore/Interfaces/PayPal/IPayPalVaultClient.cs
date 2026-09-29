using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

/// <summary>Wraps the Payment Method Tokens v3 API: vault (save) a card and delete a saved card.</summary>
public interface IPayPalVaultClient
{
    /// <summary>
    /// POST /v3/vault/payment-tokens — vaults the card directly. <paramref name="merchantCustomerId"/>
    /// is the shopper's username (sent as customer.merchant_customer_id). <paramref name="requestId"/>
    /// is a per-request idempotency key.
    /// </summary>
    Task<PayPalPaymentTokenResult> CreatePaymentTokenAsync(
        string merchantCustomerId, CardDetails card, string requestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// DELETE /v3/vault/payment-tokens/{id}. Treats a 404 (already gone) as success — the end
    /// state ("not usable to pay") already holds.
    /// </summary>
    Task DeletePaymentTokenAsync(string vaultId, CancellationToken cancellationToken = default);
}
