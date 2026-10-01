using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

/// <summary>Refunds a captured payment, in full or in part. Shopper-scoped: only the buyer who placed the order may refund it.</summary>
public class RefundOrderEndpoint : IEndpoint<IResult, RefundOrderRequest, string, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/refunds",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, RefundOrderRequest request, ClaimsPrincipal user, IPaymentService paymentService) =>
            {
                request.OrderId = orderId;
                return await HandleAsync(request, user.Identity!.Name!, paymentService);
            })
            .Produces<RefundOrderResponse>()
            .WithTags("PaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(RefundOrderRequest request, string buyerId, IPaymentService paymentService)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return Results.BadRequest("'idempotencyKey' is required.");
        }

        var (payment, refund) = await paymentService.RefundAsync(request.OrderId, buyerId, request.Amount, request.IdempotencyKey, default);

        var response = new RefundOrderResponse(request.CorrelationId())
        {
            OrderId = request.OrderId,
            RefundId = refund.Id,
            Payment = OrderPaymentDto.From(payment)
        };
        return Results.Ok(response);
    }
}
