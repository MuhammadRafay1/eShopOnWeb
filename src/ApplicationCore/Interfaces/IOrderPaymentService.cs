using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>Request to authorize (hold) an order's total — either a raw card or a saved card.</summary>
public class PaymentAuthorizationRequest
{
    /// <summary>Raw card for a one-off payment. Mutually exclusive with <see cref="PaymentMethodId"/>.</summary>
    public CardDetails? Card { get; set; }

    /// <summary>A saved card (local PaymentMethod id). Mutually exclusive with <see cref="Card"/>.</summary>
    public int? PaymentMethodId { get; set; }
}

/// <summary>Request to refund a captured order, in full or in part, under an idempotency key.</summary>
public class RefundRequest
{
    /// <summary>Caller-supplied idempotency key — a replay under the same key must not refund twice.</summary>
    public string IdempotencyKey { get; set; } = "";

    /// <summary>Amount to refund. Null means a full refund of the remaining captured amount.</summary>
    public decimal? Amount { get; set; }
}

/// <summary>
/// Orchestrates the money movement for an order across PayPal: authorize (hold), fulfil (capture),
/// cancel (void) and refund. Each shopper-scoped method verifies the caller owns the order and
/// throws <see cref="Exceptions.OrderNotFoundException"/> otherwise (same for missing vs not-owned).
/// </summary>
public interface IOrderPaymentService
{
    /// <summary>Authorize the order total (place the hold). Idempotent: a second call is a no-op.</summary>
    Task<OrderPayment> AuthorizeAsync(int orderId, string buyerId, PaymentAuthorizationRequest request);

    /// <summary>Operator action: capture the hold (take the money), renewing a stale hold if needed.</summary>
    Task<OrderPayment> FulfilAsync(int orderId);

    /// <summary>Operator action: cancel before fulfilment — voids the hold so no money moves.</summary>
    Task CancelAsync(int orderId);

    /// <summary>Refund a captured order (full or partial) under a caller idempotency key.</summary>
    Task<Refund> RefundAsync(int orderId, string buyerId, RefundRequest request);
}
