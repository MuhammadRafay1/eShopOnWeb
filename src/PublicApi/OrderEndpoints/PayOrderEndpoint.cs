using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Authorizes (holds, does not capture) the order total with PayPal, using either a one-off card
/// or one of the shopper's saved cards. Idempotent: a second call for an already-authorized order
/// returns the existing authorization instead of creating a new one.
/// </summary>
public class PayOrderEndpoint : IEndpoint<IResult, PayOrderRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/pay",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, PayOrderRequest request, IPaymentService paymentService, ClaimsPrincipal user) =>
            {
                request.OrderId = orderId;
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, paymentService);
            })
            .Produces<PayOrderResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(PayOrderRequest request, IPaymentService paymentService)
    {
        if ((request.Card == null) == (request.SavedPaymentMethodId == null))
        {
            return Results.BadRequest("Provide exactly one of 'card' or 'savedPaymentMethodId'.");
        }

        var response = new PayOrderResponse(request.CorrelationId());

        var payment = request.Card != null
            ? await paymentService.PayWithCardAsync(request.OrderId, request.BuyerId, MapCard(request.Card))
            : await paymentService.PayWithSavedCardAsync(request.OrderId, request.BuyerId, request.SavedPaymentMethodId!.Value);

        response.OrderId = request.OrderId;
        response.Payment = payment.ToDto();

        return Results.Ok(response);
    }

    private static CardDetails MapCard(PayCardDto card) => new(
        card.Name,
        card.Number,
        card.Expiry,
        card.SecurityCode,
        new PayPalBillingAddress(
            card.BillingAddress.AddressLine1,
            card.BillingAddress.AddressLine2,
            card.BillingAddress.AdminArea2,
            card.BillingAddress.AdminArea1,
            card.BillingAddress.PostalCode,
            card.BillingAddress.CountryCode));
}
