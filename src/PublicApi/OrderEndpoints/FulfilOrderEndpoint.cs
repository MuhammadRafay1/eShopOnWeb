using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.EntityFrameworkCore;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Operator action: takes the money that was held at <c>pay</c> time. A stale authorization is renewed
/// (reauthorize) and the capture retried once before giving up with an operator-actionable error.
/// </summary>
public class FulfilOrderEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId:int}/fulfil",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, IRepository<Order> orderRepository, IPayPalPaymentGateway gateway) =>
            {
                return await HandleAsync(orderId, orderRepository, gateway);
            })
            .Produces<FulfilOrderResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(int orderId, IRepository<Order> orderRepository, IPayPalPaymentGateway gateway)
    {
        var order = await orderRepository.FirstOrDefaultAsync(new OrderWithPaymentByIdSpec(orderId));
        if (order?.Payment is null)
        {
            return Results.NotFound();
        }

        try
        {
            order.BeginCapture();
        }
        catch (System.InvalidOperationException ex)
        {
            return Results.Conflict(ex.Message);
        }

        try
        {
            await orderRepository.UpdateAsync(order);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict("This order is already being fulfilled.");
        }

        var authorizationId = order.Payment.AuthorizationId!;
        var idempotencyKey = order.Payment.CaptureIdempotencyKey;

        CaptureResult capture;
        try
        {
            capture = await gateway.CaptureAsync(authorizationId, idempotencyKey, default);

            if (!capture.Success && capture.NeedsReauthorization)
            {
                var reauth = await gateway.ReauthorizeAsync(authorizationId, order.Payment.ReauthorizeIdempotencyKey, default);
                if (reauth.Success)
                {
                    capture = await gateway.CaptureAsync(authorizationId, idempotencyKey, default);
                }
                else
                {
                    order.RecordFulfilmentFailed(
                        $"Authorization can no longer be renewed and capture failed: {reauth.FailureReason}. " +
                        "A new payment is required for this order.");
                    await orderRepository.UpdateAsync(order);
                    return Results.Json(new { message = order.Payment.LastError }, statusCode: StatusCodes.Status502BadGateway);
                }
            }
        }
        catch (PaymentGatewayException ex)
        {
            order.RecordFulfilmentFailed(ex.Message);
            await orderRepository.UpdateAsync(order);
            return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status502BadGateway);
        }

        if (!capture.Success)
        {
            order.RecordFulfilmentFailed(capture.FailureReason ?? "Capture failed.");
            await orderRepository.UpdateAsync(order);
            return Results.Json(new { message = order.Payment.LastError }, statusCode: StatusCodes.Status502BadGateway);
        }

        order.RecordFulfilled(capture.CaptureId!, capture.CaptureStatus ?? "COMPLETED", capture.CapturedAmount,
            capture.FeeAmount, capture.NetAmount);
        await orderRepository.UpdateAsync(order);

        return Results.Ok(new FulfilOrderResponse(System.Guid.NewGuid())
        {
            OrderId = order.Id,
            Status = order.Status.ToString(),
            CaptureId = order.Payment.CaptureId,
            CaptureStatus = order.Payment.CaptureStatus,
            CapturedAmount = order.Payment.CapturedAmount,
            PayPalFee = order.Payment.PayPalFee,
            NetAmount = order.Payment.NetAmount
        });
    }
}
