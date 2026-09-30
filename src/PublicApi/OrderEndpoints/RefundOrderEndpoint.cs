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
/// Refunds a captured order, in full or in part, for the shopper who owns it. Idempotent by
/// the caller-supplied idempotencyKey: repeating the same key never refunds twice, while two
/// distinct keys are two legitimate partial refunds.
/// </summary>
public class RefundOrderEndpoint : IEndpoint<IResult, RefundOrderRequest, IPaymentService, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/refunds",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, RefundOrderRequest request, IPaymentService paymentService, HttpContext httpContext) =>
            {
                request.OrderId = orderId;
                return await HandleAsync(request, paymentService, httpContext);
            })
            .Produces<RefundOrderResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(RefundOrderRequest request, IPaymentService paymentService, HttpContext httpContext)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return Results.BadRequest("idempotencyKey is required.");
        }

        var buyerId = httpContext.User.Identity!.Name!;

        var result = await paymentService.RefundAsync(request.OrderId, buyerId, request.Amount, request.IdempotencyKey, httpContext.RequestAborted);

        var response = new RefundOrderResponse(request.CorrelationId())
        {
            RefundId = result.RefundId,
            Status = result.Status,
            Amount = result.Amount,
            PaymentStatus = result.OrderPaymentStatus,
            TotalRefunded = result.TotalRefunded,
            RefundableRemaining = result.RefundableRemaining
        };
        return Results.Created($"/api/orders/{request.OrderId}/refunds/{response.RefundId}", response);
    }
}
