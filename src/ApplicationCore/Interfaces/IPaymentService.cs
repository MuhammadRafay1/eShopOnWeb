using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the money movement for an order against PayPal, keeping each action separately
/// invocable and idempotent in effect. Ownership is enforced here: a <c>null</c> return means the
/// order does not exist or does not belong to the caller (the API turns that into a 404).
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Authorizes (holds) the order total with either a one-off card or a saved card. Idempotent:
    /// an already-authorized order returns its existing state without re-authorizing.
    /// </summary>
    Task<Order?> AuthorizeAsync(int orderId, string buyerId, PayPalCard? card,
        string? paymentMethodId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Captures the authorization at fulfilment (operator action). Renews a stale authorization
    /// first if needed. Idempotent: an already-fulfilled order returns its existing capture.
    /// </summary>
    Task<Order?> FulfilAsync(int orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels before fulfilment (operator action), releasing any held funds. Idempotent: an
    /// already-cancelled order returns its existing state.
    /// </summary>
    Task<Order?> CancelAsync(int orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refunds a captured payment, in full or in part. The idempotency key makes a repeat request
    /// under the same key a no-op that returns the original refund.
    /// </summary>
    Task<Refund?> RefundAsync(int orderId, string buyerId, decimal? amount, string idempotencyKey,
        CancellationToken cancellationToken = default);
}
