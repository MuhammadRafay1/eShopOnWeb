using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.EntityFrameworkCore;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Returns a captured payment, in full or in part. A repeated request under the same idempotency key
/// never refunds twice; two distinct partial refunds (different keys) remain legitimate.
/// </summary>
public class RefundOrderEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId:int}/refunds",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, RefundOrderBody body, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
             ClaimsPrincipal user, IRepository<Order> orderRepository, IPayPalPaymentGateway gateway) =>
            {
                if (string.IsNullOrWhiteSpace(idempotencyKey))
                {
                    return Results.BadRequest("An Idempotency-Key header is required.");
                }

                var buyerId = user.FindFirstValue(ClaimTypes.Name)!;
                return await HandleAsync(new RefundOrderRequest(orderId, buyerId, idempotencyKey, body), orderRepository, gateway);
            })
            .Produces<RefundOrderResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(RefundOrderRequest request, IRepository<Order> orderRepository, IPayPalPaymentGateway gateway)
    {
        var order = await orderRepository.FirstOrDefaultAsync(new OrderWithPaymentByIdForBuyerSpec(request.OrderId, request.BuyerId));
        if (order?.Payment is null)
        {
            return Results.NotFound();
        }

        var existing = order.Payment.FindRefund(request.IdempotencyKey);
        if (existing is not null)
        {
            return Results.Ok(ToResponse(request, order, existing));
        }

        var previousStatus = order.Status;

        try
        {
            order.BeginRefund();
        }
        catch (System.InvalidOperationException ex)
        {
            return Results.Conflict(ex.Message);
        }

        var amount = request.Body.Amount ?? order.Payment.RemainingRefundable();
        if (amount <= 0m || amount > order.Payment.RemainingRefundable())
        {
            return Results.BadRequest(
                $"Refund amount must be between 0 (exclusive) and {order.Payment.RemainingRefundable()} (the remaining refundable amount).");
        }

        var refund = order.Payment.AddRefundClaim(request.IdempotencyKey, amount);

        try
        {
            await orderRepository.UpdateAsync(order);
        }
        catch (DbUpdateException)
        {
            // Either a concurrent refund with the SAME key won the race (replay its result), or the
            // order's own status transition conflicted (another refund/cancel/fulfil is in flight).
            var reloaded = await orderRepository.FirstOrDefaultAsync(new OrderWithPaymentByIdForBuyerSpec(request.OrderId, request.BuyerId));
            var replay = reloaded?.Payment?.FindRefund(request.IdempotencyKey);
            if (replay is not null)
            {
                return Results.Ok(ToResponse(request, reloaded!, replay));
            }

            return Results.Conflict("A conflicting refund or status change is already in progress for this order.");
        }

        try
        {
            var result = await gateway.RefundAsync(
                new RefundGatewayRequest(order.Payment.CaptureId!, amount, order.Payment.Currency,
                    order.Payment.RefundIdempotencyKey(request.IdempotencyKey), request.Body.Reason),
                default);

            if (!result.Success)
            {
                order.RecordRefundFailed(result.FailureReason ?? "Refund failed.", previousStatus);
                await orderRepository.UpdateAsync(order);
                return Results.Json(new { message = result.FailureReason }, statusCode: StatusCodes.Status502BadGateway);
            }

            order.RecordRefunded(refund, result.RefundId!, result.Status ?? "COMPLETED", amount);
            await orderRepository.UpdateAsync(order);

            return Results.Ok(ToResponse(request, order, refund));
        }
        catch (PaymentGatewayException ex)
        {
            order.RecordRefundFailed(ex.Message, previousStatus);
            await orderRepository.UpdateAsync(order);
            return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static RefundOrderResponse ToResponse(RefundOrderRequest request, Order order, Refund refund) => new(request.CorrelationId())
    {
        RefundId = refund.Id,
        OrderId = order.Id,
        Status = order.Status.ToString(),
        PayPalRefundId = refund.PayPalRefundId,
        PayPalRefundStatus = refund.Status,
        Amount = refund.Amount
    };
}
