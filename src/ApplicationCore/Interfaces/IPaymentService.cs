using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Models.Payments;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>Orchestrates the pay / fulfil / cancel / refund lifecycle of an order's Payment.</summary>
public interface IPaymentService
{
    /// <summary>
    /// Authorizes (holds, does not capture) the order total. Exactly one of <paramref name="card"/> or
    /// <paramref name="paymentMethodId"/> must be supplied. Returns null if no such order exists for
    /// <paramref name="buyerId"/> (caller maps that to 404).
    /// </summary>
    Task<PaymentAuthorizationOutcome?> AuthorizeAsync(int orderId, string buyerId, CardDetails? card, int? paymentMethodId, CancellationToken ct = default);

    /// <summary>Captures the authorized amount, renewing a stale authorization first if needed. Returns null if no such order exists.</summary>
    Task<PaymentFulfilmentOutcome?> FulfilAsync(int orderId, CancellationToken ct = default);

    /// <summary>Voids the hold before fulfilment; no money moves. Returns null if no such order exists.</summary>
    Task<PaymentCancellationOutcome?> CancelAsync(int orderId, CancellationToken ct = default);

    /// <summary>
    /// Refunds a captured payment in full (amount: null) or in part, deduped by <paramref name="idempotencyKey"/>.
    /// Returns null if no such order exists for <paramref name="buyerId"/>.
    /// </summary>
    Task<PaymentRefundOutcome?> RefundAsync(int orderId, string buyerId, string idempotencyKey, decimal? amount, CancellationToken ct = default);

    /// <summary>The caller's own orders with their payment state.</summary>
    Task<IReadOnlyList<OrderPaymentSummary>> GetOrdersForBuyerAsync(string buyerId, CancellationToken ct = default);
}
