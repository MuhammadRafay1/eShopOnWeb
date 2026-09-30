using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates PayPal + the Order/Payment aggregate for the pay -> fulfil -> cancel/refund
/// lifecycle. Every method returns null when the order does not exist or is not owned by the
/// given buyer (buyer-scoped overloads only) so endpoints can turn that into a 404.
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Authorizes (holds) the order total, either with a one-off card or a saved card
    /// (<paramref name="paymentMethodId"/>). Idempotent: re-authorizing an already-authorized
    /// order returns the existing authorization rather than calling PayPal again.
    /// </summary>
    Task<Order?> AuthorizeAsync(int orderId, string buyerId, PayPalCardDetails? card, int? paymentMethodId,
        CancellationToken ct = default);

    /// <summary>
    /// Captures the held funds (operator/admin action). Renews a stale authorization and retries
    /// once before giving up. Idempotent: an already-fulfilled order returns the existing capture.
    /// </summary>
    Task<Order?> FulfilAsync(int orderId, CancellationToken ct = default);

    /// <summary>
    /// Releases the held funds before fulfilment (operator/admin action). Idempotent.
    /// </summary>
    Task<Order?> CancelAsync(int orderId, CancellationToken ct = default);

    /// <summary>
    /// Refunds (fully or partially) a captured payment. <paramref name="idempotencyKey"/> is
    /// caller-supplied and required: repeating it returns the original refund rather than
    /// refunding twice, while distinct keys allow legitimate split refunds.
    /// </summary>
    Task<PaymentRefund?> RefundAsync(int orderId, string buyerId, decimal? amount, string idempotencyKey,
        CancellationToken ct = default);
}
