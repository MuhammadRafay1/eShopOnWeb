using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>An order together with its payment record (payment may be null before the order is paid).</summary>
public record OrderPaymentResult(Order Order, Payment? Payment);

/// <summary>The outcome of a refund: the refund row, the (updated) payment/order, and whether the
/// request was an idempotent replay of an earlier one under the same key.</summary>
public record RefundOperationResult(Refund Refund, Payment Payment, Order Order, bool WasReplay);

public interface IPaymentService
{
    /// <summary>Authorize (hold) the order total, paying with either raw card details or a saved card.</summary>
    Task<OrderPaymentResult> PayAsync(int orderId, string buyerId, CardDetails? card, int? paymentMethodId);

    /// <summary>Operator action: capture the held funds at fulfilment, renewing a stale hold first.</summary>
    Task<OrderPaymentResult> FulfilAsync(int orderId);

    /// <summary>Operator action: release the hold before fulfilment so no money moves.</summary>
    Task<OrderPaymentResult> CancelAsync(int orderId);

    /// <summary>Operator action: refund a captured payment, in full or in part, idempotently.</summary>
    Task<RefundOperationResult> RefundAsync(int orderId, decimal? amount, string idempotencyKey);

    /// <summary>The caller's own orders paired with their payment state.</summary>
    Task<IReadOnlyList<OrderPaymentResult>> GetMyOrdersAsync(string buyerId);
}
