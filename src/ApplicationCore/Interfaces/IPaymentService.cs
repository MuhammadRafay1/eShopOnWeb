using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderPaymentAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the payment lifecycle of a single order: load the order, verify ownership, transition
/// the OrderPayment, take a duplicate-prevention claim, call <see cref="IPayPalPaymentGateway"/>, and
/// persist the result. Endpoints stay thin and call straight through to this.
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Authorizes (holds) the order total. Exactly one of <paramref name="card"/> or
    /// <paramref name="paymentMethodId"/> must be supplied.
    /// </summary>
    Task<OrderPayment> PayAsync(int orderId, string buyerId, PayPalCardInput? card, int? paymentMethodId, CancellationToken cancellationToken);

    /// <summary>Captures (takes) the held funds. Renews a stale authorization first if needed.</summary>
    Task<OrderPayment> FulfilAsync(int orderId, CancellationToken cancellationToken);

    /// <summary>Releases a hold before fulfilment - no money ever moves.</summary>
    Task<OrderPayment> CancelAsync(int orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Refunds a captured payment, in full (<paramref name="amount"/> null) or in part. Repeating the
    /// same <paramref name="idempotencyKey"/> returns the original refund rather than refunding twice.
    /// </summary>
    Task<(OrderPayment Payment, OrderRefund Refund)> RefundAsync(int orderId, string buyerId, decimal? amount, string idempotencyKey, CancellationToken cancellationToken);
}
