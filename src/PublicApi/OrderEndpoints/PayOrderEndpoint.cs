using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Authorizes (holds) an order's total with PayPal - either a one-off card or a saved payment
/// method. Does not take the money; that happens at fulfilment (capture).
/// </summary>
public class PayOrderEndpoint : IEndpoint<IResult, PayOrderRequest, IPaymentService, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/pay",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, PayOrderRequest request, IPaymentService paymentService, HttpContext httpContext) =>
            {
                request.OrderId = orderId;
                return await HandleAsync(request, paymentService, httpContext);
            })
            .Produces<PayOrderResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(PayOrderRequest request, IPaymentService paymentService, HttpContext httpContext)
    {
        if ((request.Card is null) == (request.PaymentMethodId is null))
        {
            return Results.BadRequest("Provide exactly one of 'card' or 'paymentMethodId'.");
        }

        var buyerId = httpContext.User.Identity!.Name!;

        var input = new PaymentAuthorizeInput
        {
            Card = request.Card?.ToPayPalCardInput(),
            PaymentMethodId = request.PaymentMethodId,
            SaveCard = request.SaveCard
        };

        var payment = await paymentService.AuthorizeAsync(request.OrderId, buyerId, input, httpContext.RequestAborted);

        var response = new PayOrderResponse(request.CorrelationId())
        {
            OrderId = request.OrderId,
            PaymentStatus = "Authorized",
            AuthorizationId = payment.AuthorizationId,
            AuthorizedAmount = payment.AuthorizedAmount,
            Currency = payment.CurrencyCode,
            AuthorizationExpiresAt = payment.AuthorizationExpiresAt
        };
        return Results.Ok(response);
    }
}
