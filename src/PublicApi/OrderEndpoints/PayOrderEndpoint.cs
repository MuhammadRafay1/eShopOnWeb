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
/// Authorizes (holds) the order total with a one-off card or a saved card. Does not take the
/// money yet - that happens at fulfilment. Idempotent: re-paying an already-authorized order
/// returns the existing authorization.
/// </summary>
public class PayOrderEndpoint : IEndpoint<IResult, PayOrderRequest, ClaimsPrincipal, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/pay",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, PayOrderRequest request, ClaimsPrincipal user, IPaymentService paymentService) =>
            {
                request.OrderId = orderId;
                return await HandleAsync(request, user, paymentService);
            })
            .Produces<PayOrderResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(PayOrderRequest request, ClaimsPrincipal user, IPaymentService paymentService)
    {
        var response = new PayOrderResponse(request.CorrelationId());

        var buyerId = user.Identity?.Name;
        if (string.IsNullOrEmpty(buyerId)) return Results.Unauthorized();

        PayPalCardDetails? card = null;
        if (request.PaymentMethodId is null)
        {
            if (string.IsNullOrWhiteSpace(request.CardNumber) || string.IsNullOrWhiteSpace(request.ExpiryYearMonth))
            {
                return Results.BadRequest("Either paymentMethodId or card details (cardNumber, expiryYearMonth) must be supplied.");
            }
            card = new PayPalCardDetails(
                request.CardNumber!,
                request.ExpiryYearMonth!,
                request.CardholderName ?? "",
                request.AddressLine1 ?? "",
                request.City ?? "",
                request.State ?? "",
                request.PostalCode ?? "",
                request.CountryCode ?? "US");
        }

        var order = await paymentService.AuthorizeAsync(request.OrderId, buyerId, card, request.PaymentMethodId);
        if (order is null) return Results.NotFound();

        response.OrderId = order.Id;
        response.Status = order.Status.ToString();
        response.Payment = PaymentDto.FromEntity(order.Payment);

        return Results.Ok(response);
    }
}
