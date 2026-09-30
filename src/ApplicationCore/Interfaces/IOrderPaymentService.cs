using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Payments;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public interface IOrderPaymentService
{
    Task<Order> PlaceOrderAsync(string buyerId, IReadOnlyList<OrderLineRequest> items, Address? shipToAddress, CancellationToken ct);

    Task<Order?> GetOrderForBuyerAsync(int orderId, string buyerId, CancellationToken ct);

    Task<IReadOnlyList<Order>> GetOrdersForBuyerAsync(string buyerId, CancellationToken ct);

    /// <summary>Authorizes (holds) the order total. Returns null if the order does not exist or is not owned by buyerId.</summary>
    Task<Order?> PayAsync(int orderId, string buyerId, CardInput? card, int? savedPaymentMethodId, CancellationToken ct);

    /// <summary>Captures a held authorization. Returns null if the order does not exist. Admin-only caller scope enforced by the endpoint.</summary>
    Task<Order?> FulfilAsync(int orderId, CancellationToken ct);

    /// <summary>Voids a held authorization before fulfilment. Returns null if the order does not exist. Admin-only caller scope enforced by the endpoint.</summary>
    Task<Order?> CancelAsync(int orderId, CancellationToken ct);

    /// <summary>Refunds a captured payment, full or partial. Returns null if the order does not exist or is not owned by buyerId.</summary>
    Task<(Order Order, PaymentRefund Refund)?> RefundAsync(int orderId, string buyerId, decimal? amount, string idempotencyKey, CancellationToken ct);
}
