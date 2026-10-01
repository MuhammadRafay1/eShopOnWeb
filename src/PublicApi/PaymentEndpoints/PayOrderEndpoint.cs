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

/// <summary>
/// Authorizes (holds) the order total with PayPal - does not take the money. Pays with a one-off card
/// or a previously saved card (paymentMethodId), never both.
/// </summary>
public class PayOrderEndpoint : IEndpoint<IResult, PayOrderRequest, string, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/pay",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, PayOrderRequest request, ClaimsPrincipal user, IPaymentService paymentService) =>
            {
                request.OrderId = orderId;
                return await HandleAsync(request, user.Identity!.Name!, paymentService);
            })
            .Produces<PayOrderResponse>()
            .WithTags("PaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(PayOrderRequest request, string buyerId, IPaymentService paymentService)
    {
        var hasCard = request.Card is not null;
        var hasPaymentMethod = request.PaymentMethodId is not null;
        if (hasCard == hasPaymentMethod)
        {
            return Results.BadRequest("Supply exactly one of 'card' or 'paymentMethodId'.");
        }

        PayPalCardInput? card = null;
        if (request.Card is not null)
        {
            card = new PayPalCardInput(
                request.Card.Number,
                request.Card.Expiry,
                request.Card.SecurityCode,
                request.Card.CardholderName,
                request.Card.AddressLine1,
                request.Card.AddressLine2,
                request.Card.AdminArea1,
                request.Card.AdminArea2,
                request.Card.PostalCode,
                request.Card.CountryCode);
        }

        var payment = await paymentService.PayAsync(request.OrderId, buyerId, card, request.PaymentMethodId, default);

        var response = new PayOrderResponse(request.CorrelationId())
        {
            OrderId = request.OrderId,
            Payment = OrderPaymentDto.From(payment)
        };
        return Results.Ok(response);
    }
}
