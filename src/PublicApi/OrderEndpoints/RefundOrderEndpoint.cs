using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class RefundOrderRequest
{
    public int OrderId { get; set; }
    public string BuyerId { get; set; } = string.Empty;

    /// <summary>Omit for a full refund of the remaining captured amount.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Required. Repeating a request under the same key never refunds twice.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;
}

public class RefundOrderResponse
{
    public string RefundId { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal RefundedTotal { get; set; }
    public decimal RemainingRefundable { get; set; }
}

/// <summary>
/// Refunds a captured payment, in full or in part, using a caller-supplied idempotency key so a
/// retried request never refunds twice while two genuinely distinct partial refunds both succeed.
/// A partly-refunded order can never become refundable beyond what was actually captured.
/// </summary>
public class RefundOrderEndpoint : IEndpoint<IResult, RefundOrderRequest, OrderEndpointServices>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/refunds",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, RefundOrderRequest request, ClaimsPrincipal user, OrderEndpointServices services) =>
            {
                request.OrderId = orderId;
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, services);
            })
            .Produces<RefundOrderResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(RefundOrderRequest request, OrderEndpointServices services)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return Results.BadRequest("idempotencyKey is required.");
        if (request.Amount is <= 0)
            return Results.BadRequest("amount, when provided, must be positive.");

        var order = await services.OrderRepository.GetByIdAsync(request.OrderId);
        if (order is null || order.BuyerId != request.BuyerId)
            return Results.NotFound();

        if (order.Status != OrderStatus.Fulfilled && order.Status != OrderStatus.PartiallyRefunded)
            return Results.Conflict($"Order {order.Id} is {order.Status}; only a fulfilled order can be refunded.");

        var payment = await services.PaymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpecification(request.OrderId));
        if (payment?.CaptureId is null)
            return Results.Conflict($"Order {order.Id} has no recorded capture to refund.");

        var existingRefund = payment.FindRefundByKey(request.IdempotencyKey);
        if (existingRefund is not null)
        {
            return Results.Ok(new RefundOrderResponse
            {
                RefundId = existingRefund.RefundId,
                OrderId = order.Id,
                Status = order.Status.ToString(),
                Amount = existingRefund.Amount,
                RefundedTotal = payment.RefundedTotal,
                RemainingRefundable = payment.RemainingRefundable
            });
        }

        var requestedAmount = request.Amount ?? payment.RemainingRefundable;
        if (requestedAmount <= 0 || requestedAmount > payment.RemainingRefundable + 0.005m)
            return Results.Conflict($"Refund of {requestedAmount} exceeds the remaining refundable amount of {payment.RemainingRefundable} for order {order.Id}.");

        PayPalRefundResult refund;
        try
        {
            refund = await services.PaymentGateway.RefundAsync(new PayPalRefundRequest
            {
                CaptureId = payment.CaptureId,
                Amount = request.Amount,
                OrderId = order.Id.ToString(),
                InvoiceId = payment.InvoiceId,
                IdempotencyKey = request.IdempotencyKey
            });
        }
        catch (PayPalGatewayException ex)
        {
            return Results.Json(new { message = ex.Message, debugId = ex.DebugId }, statusCode: StatusCodes.Status502BadGateway);
        }

        payment.AddRefund(refund.RefundId, refund.Amount, refund.Status, request.IdempotencyKey, refund.TotalRefunded);
        await services.PaymentRepository.UpdateAsync(payment);

        var isFullRefund = payment.RemainingRefundable <= 0.005m;
        order.MarkRefunded(isFullRefund);
        await services.OrderRepository.UpdateAsync(order);

        return Results.Created($"api/orders/{order.Id}/refunds/{refund.RefundId}", new RefundOrderResponse
        {
            RefundId = refund.RefundId,
            OrderId = order.Id,
            Status = order.Status.ToString(),
            Amount = refund.Amount,
            RefundedTotal = payment.RefundedTotal,
            RemainingRefundable = payment.RemainingRefundable
        });
    }
}
