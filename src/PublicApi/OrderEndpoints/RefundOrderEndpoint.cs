using System.Security.Claims;
using System.Threading.Tasks;
using Ardalis.Result;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Refunds a fulfilled order's captured payment, in full or in part. Carries a caller-supplied
/// idempotency key: repeating the request under the same key returns the original refund rather than
/// refunding again, while two distinct keys are two legitimate partial refunds (bounded by what was
/// captured).
/// </summary>
public class RefundOrderEndpoint : IEndpoint<IResult, RefundOrderRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app) =>
        app.MapPost("api/orders/{orderId}/refunds",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, RefundOrderRequest request, ClaimsPrincipal user, HttpContext http, IPaymentService paymentService) =>
            {
                request.OrderId = orderId;
                request.BuyerId = user.Identity!.Name!;
                if (string.IsNullOrWhiteSpace(request.IdempotencyKey) && http.Request.Headers.TryGetValue("Idempotency-Key", out var headerKey))
                {
                    request.IdempotencyKey = headerKey.ToString();
                }
                return await HandleAsync(request, paymentService);
            })
            .Produces<RefundOrderResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("OrderEndpoints");

    public async Task<IResult> HandleAsync(RefundOrderRequest request, IPaymentService paymentService)
    {
        var response = new RefundOrderResponse(request.CorrelationId());

        var result = await paymentService.RefundAsync(request.OrderId, request.BuyerId, request.Amount, request.IdempotencyKey);
        if (result.Status != ResultStatus.Ok)
        {
            return result.ToErrorResult();
        }

        response.RefundId = result.Value.Refund.PayPalRefundId;
        response.Amount = result.Value.Refund.Amount;
        response.Status = result.Value.Refund.Status;
        response.Order = OrderDto.FromDomain(result.Value.Order);
        return Results.Ok(response);
    }
}
