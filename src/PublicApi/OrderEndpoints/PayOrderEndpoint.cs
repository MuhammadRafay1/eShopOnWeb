using System.Security.Claims;
using System.Threading;
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
/// Authorizes (holds) the order total. Carries card details for a one-off payment, or names one
/// of the shopper's saved cards. The money is held, not taken — capture happens at fulfilment.
/// </summary>
public class PayOrderEndpoint : IEndpoint<IResult, PayOrderRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/pay",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, PayOrderRequest request, ClaimsPrincipal user, IPaymentService paymentService) =>
            {
                request.OrderId = orderId;
                request.BuyerId = CallerIdentity.GetBuyerId(user);
                return await HandleAsync(request, paymentService);
            })
            .Produces<OrderSummaryDto>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(PayOrderRequest request, IPaymentService paymentService)
    {
        if (string.IsNullOrEmpty(request.BuyerId))
            return Results.Unauthorized();

        var hasCard = request.Card is not null;
        var hasSavedCard = !string.IsNullOrWhiteSpace(request.PaymentMethodId);
        if (hasCard == hasSavedCard)
            return Results.BadRequest("Provide exactly one of 'card' or 'paymentMethodId'.");

        PayPalCard? card = hasCard ? ToPayPalCard(request.Card!) : null;

        var order = await paymentService.AuthorizeAsync(
            request.OrderId, request.BuyerId, card, request.PaymentMethodId, CancellationToken.None);

        if (order is null)
            return Results.NotFound();

        return Results.Ok(OrderSummaryDto.From(order));
    }

    private static PayPalCard ToPayPalCard(CardDto card)
    {
        PayPalBillingAddress? billing = null;
        if (card.BillingAddress is not null)
        {
            var a = card.BillingAddress;
            billing = new PayPalBillingAddress(
                AddressLine1: a.AddressLine1,
                AddressLine2: a.AddressLine2,
                AdminArea2: a.City,
                AdminArea1: a.State,
                PostalCode: a.PostalCode,
                CountryCode: a.CountryCode ?? string.Empty);
        }

        return new PayPalCard(
            Name: card.Name ?? string.Empty,
            Number: card.Number ?? string.Empty,
            Expiry: card.Expiry ?? string.Empty,
            SecurityCode: card.Cvv ?? string.Empty,
            BillingAddress: billing);
    }
}
