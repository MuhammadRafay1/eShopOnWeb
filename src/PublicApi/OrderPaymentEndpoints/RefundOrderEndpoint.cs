using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

/// <summary>
/// Refunds a captured payment, in full or in part. Idempotent per caller-supplied idempotencyKey.
/// </summary>
public class RefundOrderEndpoint : IEndpoint<IResult, RefundOrderRequest, IOrderPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/refunds",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, RefundOrderRequest request, ClaimsPrincipal user, IOrderPaymentService orderPaymentService) =>
            {
                request.OrderId = orderId;
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, orderPaymentService);
            })
            .Produces<RefundOrderResponse>()
            .WithTags("OrderPaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(RefundOrderRequest request, IOrderPaymentService orderPaymentService)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return Results.BadRequest("idempotencyKey is required.");
        }

        var result = await orderPaymentService.RefundAsync(request.OrderId, request.BuyerId, request.Amount, request.IdempotencyKey, CancellationToken.None);
        if (result is null)
        {
            return Results.NotFound();
        }

        var (order, refund) = result.Value;
        var response = new RefundOrderResponse(request.CorrelationId())
        {
            RefundId = refund.Id,
            OrderId = order.Id,
            Amount = refund.Amount,
            PaymentStatus = order.PaymentStatus.ToString(),
            TotalRefunded = order.Payment!.TotalRefunded(),
            RefundableRemaining = order.Payment!.RefundableRemaining()
        };

        return Results.Created($"api/orders/{order.Id}/refunds/{refund.Id}", response);
    }
}
