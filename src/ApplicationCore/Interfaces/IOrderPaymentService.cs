using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PaymentGateway;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the payment lifecycle for an order. One method per caller-invocable action, so route-level
/// and service-level separation match 1:1. All state-machine, idempotency and ownership rules live here.
/// </summary>
public interface IOrderPaymentService
{
    /// <summary>Authorizes (holds) the order total. Exactly one of <paramref name="card"/> / saved card must be given.</summary>
    Task<AuthorizeActionResult> AuthorizeAsync(
        int orderId, string buyerId, CardDetails? card, int? savedPaymentMethodId, CancellationToken ct);

    /// <summary>Operator action: captures the held funds, renewing a stale hold first if needed.</summary>
    Task<FulfilActionResult> FulfilAsync(int orderId, CancellationToken ct);

    /// <summary>Operator action: cancels before fulfilment, releasing any held funds.</summary>
    Task<CancelActionResult> CancelAsync(int orderId, CancellationToken ct);

    /// <summary>Refunds a captured payment, full or partial, deduplicated by the caller-supplied key.</summary>
    Task<RefundActionResult> RefundAsync(
        int orderId, string buyerId, decimal? amount, string idempotencyKey, CancellationToken ct);
}

public record AuthorizeActionResult(
    int OrderId,
    OrderStatus Status,
    decimal AuthorizedAmount,
    string Currency,
    DateTimeOffset AuthorizationExpiresAt);

public record FulfilActionResult(
    int OrderId,
    OrderStatus Status,
    decimal CapturedAmount,
    decimal? PayPalFee,
    decimal? NetAmount,
    string Currency);

public record CancelActionResult(int OrderId, OrderStatus Status);

public record RefundActionResult(
    int RefundId,
    string PayPalRefundId,
    int OrderId,
    decimal Amount,
    OrderStatus Status,
    decimal RemainingRefundable);
