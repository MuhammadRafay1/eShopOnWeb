using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the money movement that follows a placed order: place → authorize (pay) → fulfil
/// (capture) / cancel (void) / refund. Shopper-scoped operations act only on the caller's own data;
/// fulfil and cancel are operator actions and take no buyer scope.
/// </summary>
public interface IOrderPaymentService
{
    /// <summary>Places an order from catalog items and quantities, awaiting payment. Returns the new order id.</summary>
    Task<int> PlaceOrderAsync(string buyerId, IReadOnlyList<OrderLineInput> items, ShippingAddressInput? address, CancellationToken cancellationToken = default);

    /// <summary>Authorizes (holds) the order total against a one-off or saved card. Idempotent per order.</summary>
    Task<OrderPaymentView> PayAsync(string buyerId, int orderId, PayInput input, CancellationToken cancellationToken = default);

    /// <summary>Operator action: fulfils the order, capturing the held funds (renewing a stale authorization first).</summary>
    Task<OrderPaymentView> FulfilAsync(int orderId, CancellationToken cancellationToken = default);

    /// <summary>Operator action: cancels the order before fulfilment, releasing the held funds.</summary>
    Task<OrderPaymentView> CancelAsync(int orderId, CancellationToken cancellationToken = default);

    /// <summary>Refunds the captured payment, in full or in part, under a caller-supplied idempotency key.</summary>
    Task<RefundLineView> RefundAsync(string buyerId, int orderId, RefundInput input, CancellationToken cancellationToken = default);

    /// <summary>The caller's orders with their payment state.</summary>
    Task<IReadOnlyList<OrderPaymentView>> GetMyOrdersAsync(string buyerId, CancellationToken cancellationToken = default);
}
