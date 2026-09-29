using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Operator action: refunds a captured payment, in full or in part. The caller-supplied
/// idempotencyKey makes a repeated request return the same refund rather than issuing a second one.
/// </summary>
public class RefundOrderEndpoint : IEndpoint<IResult, RefundOrderRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId:int}/refunds",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
                AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, RefundOrderRequest request, IPaymentService paymentService) =>
            {
                request.OrderId = orderId;
                return await HandleAsync(request, paymentService);
            })
            .Produces<RefundResponse>(StatusCodes.Status201Created)
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(RefundOrderRequest request, IPaymentService paymentService)
    {
        var result = await paymentService.RefundAsync(request.OrderId, request.Amount, request.IdempotencyKey);

        var response = new RefundResponse
        {
            RefundId = result.Refund.Id,
            PayPalRefundId = result.Refund.PayPalRefundId,
            OrderId = request.OrderId,
            Amount = result.Refund.Amount,
            Status = result.Refund.Status,
            TotalRefunded = result.Payment.RefundedAmount,
            OrderStatus = result.Order.Status.ToString(),
        };

        // A replay under the same idempotency key returns the same refund (200), not a new one (201).
        return result.WasReplay
            ? Results.Ok(response)
            : Results.Created($"api/orders/{request.OrderId}/refunds/{response.RefundId}", response);
    }
}
