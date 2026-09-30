using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

/// <summary>Shopper action: refunds a captured payment, in full or in part, deduped by a caller-supplied idempotency key.</summary>
public class RefundOrderEndpoint : IEndpoint<IResult, RefundOrderRequest, ClaimsPrincipal, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/refunds",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, RefundOrderBody body, ClaimsPrincipal user, IPaymentService paymentService) =>
            {
                var request = new RefundOrderRequest(orderId, body.IdempotencyKey, body.Amount);
                return await HandleAsync(request, user, paymentService);
            })
            .Produces<RefundOrderResponse>(StatusCodes.Status201Created)
            .WithTags("OrderPaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(RefundOrderRequest request, ClaimsPrincipal user, IPaymentService paymentService)
    {
        var buyerId = user.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        var outcome = await paymentService.RefundAsync(request.OrderId, buyerId, request.IdempotencyKey, request.Amount);
        if (outcome is null)
        {
            return Results.NotFound();
        }

        var response = new RefundOrderResponse(request.CorrelationId())
        {
            RefundId = outcome.RefundId,
            OrderId = outcome.OrderId,
            Amount = outcome.Amount,
            Status = outcome.Status,
            TotalRefunded = outcome.TotalRefunded,
            CapturedAmount = outcome.CapturedAmount
        };

        return Results.Created($"api/orders/{outcome.OrderId}/refunds/{outcome.RefundId}", response);
    }
}
