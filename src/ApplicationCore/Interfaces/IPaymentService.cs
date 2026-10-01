using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Payments;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>Orchestrates the money movement for an order: authorize, fulfil (capture), cancel (void), refund.</summary>
public interface IPaymentService
{
    /// <summary>
    /// Authorize (hold) the order total. Supply either <paramref name="card"/> (one-off) or
    /// <paramref name="paymentMethodId"/> (a saved card owned by <paramref name="buyerId"/>), not both.
    /// Idempotent: a repeat on an already-authorized order returns the existing state without re-charging.
    /// </summary>
    Task<Order> AuthorizeAsync(int orderId, string buyerId, CardDetails? card, int? paymentMethodId, CancellationToken cancellationToken);

    /// <summary>Fulfil the order — capture the held funds. Renews a stale authorization first if needed. (operator)</summary>
    Task<Order> FulfilAsync(int orderId, CancellationToken cancellationToken);

    /// <summary>Cancel before fulfilment — void the hold, releasing the funds. (operator)</summary>
    Task<Order> CancelAsync(int orderId, CancellationToken cancellationToken);

    /// <summary>Refund a captured payment in full (null amount) or in part, under a caller idempotency key. (operator)</summary>
    Task<Refund> RefundAsync(int orderId, decimal? amount, string idempotencyKey, CancellationToken cancellationToken);
}
