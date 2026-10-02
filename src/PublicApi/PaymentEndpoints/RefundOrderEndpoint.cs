using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

/// <summary>POST /api/orders/{orderId}/refunds — refunds the captured payment, in full or in part.</summary>
public class RefundOrderEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId:int}/refunds",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                int orderId,
                RefundOrderRequest request,
                ClaimsPrincipal user,
                IOrderPaymentService service,
                CancellationToken cancellationToken) =>
            {
                var buyerId = PaymentUser.BuyerId(user);
                if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
                {
                    throw new PaymentValidationException("An idempotency key is required for refunds.");
                }

                var input = new RefundInput { Amount = request.Amount, IdempotencyKey = request.IdempotencyKey };
                var refund = await service.RefundAsync(buyerId, orderId, input, cancellationToken);
                return Results.Ok(new { refundId = refund.RefundId, refund });
            })
            .WithTags("PaymentEndpoints");
    }
}
