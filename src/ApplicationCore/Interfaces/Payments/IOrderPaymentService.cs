using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;

/// <summary>
/// Orchestrates the pay -> fulfil -> cancel/refund order lifecycle: application-level state
/// machine + ownership enforcement + idempotency, on top of <see cref="IPayPalPaymentGateway"/>.
/// </summary>
public interface IOrderPaymentService
{
    Task<int> PlaceOrderAsync(string buyerId, IReadOnlyList<OrderLineItemRequest> items, ShipToAddressRequest? shipTo, CancellationToken cancellationToken);

    Task<OrderSummaryView> PayAsync(int orderId, string buyerId, PayWithRequest payWith, CancellationToken cancellationToken);

    /// <summary>Operator action: captures the authorized payment, renewing it first if it has gone stale.</summary>
    Task<OrderSummaryView> FulfilAsync(int orderId, CancellationToken cancellationToken);

    /// <summary>Operator action: releases the held funds before fulfilment.</summary>
    Task<OrderSummaryView> CancelAsync(int orderId, CancellationToken cancellationToken);

    /// <summary>Operator action: refunds the captured payment, in full (amount null) or in part.</summary>
    Task<RefundOutcome> RefundAsync(int orderId, decimal? amount, string idempotencyKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrderSummaryView>> GetOrdersForBuyerAsync(string buyerId, CancellationToken cancellationToken);

    Task<OrderSummaryView> GetOrderForBuyerAsync(int orderId, string buyerId, CancellationToken cancellationToken);
}
