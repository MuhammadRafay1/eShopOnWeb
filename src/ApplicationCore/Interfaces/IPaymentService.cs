using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public record RefundSummary(string RefundId, decimal Amount, string Status);

public record PaymentActionResult(
    int OrderId,
    string Status,
    string Currency,
    decimal Amount,
    string? AuthorizationId,
    string? CaptureId,
    decimal? CapturedAmount,
    decimal? PayPalFee,
    decimal? NetAmount,
    IReadOnlyCollection<RefundSummary> Refunds);

public record RefundOutcome(string RefundId, string Status, decimal Amount, decimal TotalRefunded);

/// <summary>
/// Drives the payment/fulfilment lifecycle of a single order's Payment: authorize (hold),
/// fulfil (capture), cancel (void), refund. Every method is idempotent in effect - a repeat
/// call never moves money twice.
/// </summary>
public interface IPaymentService
{
    Task<PaymentActionResult> AuthorizePaymentAsync(int orderId, string buyerId, CardDetails? card, int? savedCardId, CancellationToken ct = default);

    Task<PaymentActionResult> FulfilOrderAsync(int orderId, CancellationToken ct = default);

    Task<PaymentActionResult> CancelOrderAsync(int orderId, CancellationToken ct = default);

    Task<RefundOutcome> RefundOrderAsync(int orderId, string buyerId, decimal? amount, string idempotencyKey, CancellationToken ct = default);

    Task<PaymentActionResult> GetPaymentForBuyerAsync(int orderId, string buyerId, CancellationToken ct = default);

    Task<IReadOnlyList<(int OrderId, DateTimeOffset OrderDate, PaymentActionResult Payment)>> GetOrdersForBuyerAsync(string buyerId, CancellationToken ct = default);
}
