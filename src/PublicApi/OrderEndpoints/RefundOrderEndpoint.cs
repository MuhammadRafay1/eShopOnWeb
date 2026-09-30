using System.Security.Claims;
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
/// Refunds a captured payment, in full or in part, for the order's own buyer. The idempotency key
/// is caller-supplied: repeating the same key returns the original refund unchanged, while two
/// distinct keys produce two legitimate partial refunds (capped at the captured amount).
/// </summary>
public class RefundOrderEndpoint : IEndpoint<IResult, RefundOrderRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/refunds",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, RefundOrderRequest request, IPaymentService paymentService, ClaimsPrincipal user) =>
            {
                request.OrderId = orderId;
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, paymentService);
            })
            .Produces<RefundOrderResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(422)
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(RefundOrderRequest request, IPaymentService paymentService)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return Results.BadRequest("'idempotencyKey' is required.");
        }

        if (request.Amount is <= 0)
        {
            return Results.BadRequest("'amount', when specified, must be greater than zero.");
        }

        var response = new RefundOrderResponse(request.CorrelationId());

        var (payment, refund) = await paymentService.RefundAsync(request.OrderId, request.BuyerId, request.Amount, request.IdempotencyKey, request.Note);

        response.OrderId = request.OrderId;
        response.RefundId = refund.PayPalRefundId;
        response.RefundedAmount = refund.Amount;
        response.RefundStatus = refund.Status;
        response.RemainingRefundable = (payment.CapturedAmount ?? 0m) - payment.TotalRefunded();
        response.Payment = payment.ToDto();

        return Results.Created($"api/orders/{request.OrderId}/refunds/{refund.PayPalRefundId}", response);
    }
}
