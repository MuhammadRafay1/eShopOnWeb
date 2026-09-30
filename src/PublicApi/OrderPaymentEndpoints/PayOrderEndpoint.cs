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

/// <summary>Authorizes (holds, does not capture) an order's total, by card or by a saved payment method.</summary>
public class PayOrderEndpoint : IEndpoint<IResult, PayOrderRequest, ClaimsPrincipal, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/pay",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, PayOrderBody body, ClaimsPrincipal user, IPaymentService paymentService) =>
            {
                var request = new PayOrderRequest(orderId, body.Card, body.PaymentMethodId);
                return await HandleAsync(request, user, paymentService);
            })
            .Produces<PayOrderResponse>()
            .WithTags("OrderPaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(PayOrderRequest request, ClaimsPrincipal user, IPaymentService paymentService)
    {
        var buyerId = user.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        var outcome = await paymentService.AuthorizeAsync(request.OrderId, buyerId, request.Card, request.PaymentMethodId);
        if (outcome is null)
        {
            return Results.NotFound();
        }

        var response = new PayOrderResponse(request.CorrelationId())
        {
            OrderId = outcome.OrderId,
            Status = outcome.Status,
            AuthorizationId = outcome.AuthorizationId,
            HeldAmount = outcome.HeldAmount,
            Currency = outcome.CurrencyCode
        };

        return Results.Ok(response);
    }
}
