using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.Infrastructure.Payments.PayPal;
using Microsoft.Extensions.Options;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Returns money after fulfilment - a full or partial refund of the captured payment.
/// Shopper-scoped (own order only), per the task's explicit list of operator actions (fulfil,
/// cancel, reconciliation) which does not include refunds. The caller-supplied idempotency key
/// makes a repeated request under the same key return the original result rather than refund
/// twice; two distinct partial refunds remain legitimate.
/// </summary>
public class RefundOrderEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/refunds",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                int orderId,
                RefundOrderRequest request,
                IRepository<Order> orderRepository,
                IRepository<Payment> paymentRepository,
                IPayPalPaymentGateway gateway,
                IOptions<PayPalOptions> payPalOptions,
                ClaimsPrincipal user,
                CancellationToken ct) =>
            {
                return await HandleAsync(orderId, request, orderRepository, paymentRepository,
                    gateway, payPalOptions, user, ct);
            })
            .Produces<RefundOrderResponse>()
            .WithTags("OrderEndpoints");
    }

    private static async Task<IResult> HandleAsync(int orderId, RefundOrderRequest request,
        IRepository<Order> orderRepository, IRepository<Payment> paymentRepository,
        IPayPalPaymentGateway gateway, IOptions<PayPalOptions> payPalOptions,
        ClaimsPrincipal user, CancellationToken ct)
    {
        var buyerId = user.Identity!.Name!;
        var currency = payPalOptions.Value.Currency;

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return Results.BadRequest("An idempotencyKey is required.");
        }

        var order = await orderRepository.FirstOrDefaultAsync(new OrderWithItemsByIdSpec(orderId), ct);
        if (order is null || order.BuyerId != buyerId)
        {
            throw new OrderNotFoundException(orderId);
        }

        if (order.Status != OrderStatus.Fulfilled && order.Status != OrderStatus.PartiallyRefunded)
        {
            throw new InvalidOrderStateException(
                $"Order {orderId} cannot be refunded from status {order.Status}; a refund is only allowed after fulfilment.");
        }

        var payment = await paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpecification(orderId), ct);
        if (payment is null || payment.CaptureId is null)
        {
            throw new InvalidOrderStateException($"Order {orderId} has no captured payment to refund.");
        }

        // Local idempotency check first: a repeat under the same key returns the stored result and
        // never calls PayPal again.
        var existing = payment.FindRefundByIdempotencyKey(request.IdempotencyKey);
        if (existing is not null)
        {
            return Results.Ok(BuildResponse(order, payment, existing));
        }

        var amount = request.Amount ?? payment.RemainingRefundable;
        if (amount <= 0)
        {
            return Results.BadRequest("Refund amount must be greater than zero.");
        }
        if (amount > payment.RemainingRefundable)
        {
            throw new RefundLimitExceededException(
                $"Refund of {amount} exceeds the remaining refundable balance {payment.RemainingRefundable} for order {orderId}.");
        }

        var outcome = await gateway.RefundAsync(payment.CaptureId, amount, currency,
            request.IdempotencyKey, ct);

        var refund = payment.AddRefund(outcome.RefundId, amount, outcome.Status, request.IdempotencyKey);
        order.MarkRefunded(partial: payment.RefundedAmount < payment.CapturedGrossAmount);

        await paymentRepository.UpdateAsync(payment, ct);
        await orderRepository.UpdateAsync(order, ct);

        return Results.Ok(BuildResponse(order, payment, refund));
    }

    private static RefundOrderResponse BuildResponse(Order order, Payment payment, Refund refund) => new()
    {
        RefundId = refund.Id,
        PayPalRefundId = refund.PayPalRefundId,
        Status = refund.Status,
        Amount = refund.Amount,
        RefundedTotal = payment.RefundedAmount,
        CapturedGrossAmount = payment.CapturedGrossAmount,
        OrderStatus = order.Status.ToString()
    };
}

public class RefundOrderRequest
{
    /// <summary>Amount to refund; omit for the full remaining balance.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Caller-supplied idempotency key (required).</summary>
    public string IdempotencyKey { get; set; } = string.Empty;
}

public class RefundOrderResponse
{
    public int RefundId { get; set; }
    public string PayPalRefundId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal RefundedTotal { get; set; }
    public decimal? CapturedGrossAmount { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
}
