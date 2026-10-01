using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

/// <summary>
/// Saves a card to PayPal's vault for the signed-in shopper, for reuse on a later order. Full card
/// details are never stored by this application - only the safe descriptor PayPal returns.
/// </summary>
public class SavePaymentMethodEndpoint : IEndpoint<IResult, SavePaymentMethodRequest, string, IPaymentMethodService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (SavePaymentMethodRequest request, ClaimsPrincipal user, IPaymentMethodService paymentMethodService) =>
            {
                return await HandleAsync(request, user.Identity!.Name!, paymentMethodService);
            })
            .Produces<SavePaymentMethodResponse>()
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(SavePaymentMethodRequest request, string buyerId, IPaymentMethodService paymentMethodService)
    {
        var card = new PayPalCardInput(
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

        var paymentMethod = await paymentMethodService.SaveCardAsync(buyerId, card, default);

        var response = new SavePaymentMethodResponse(request.CorrelationId())
        {
            PaymentMethodId = paymentMethod.Id,
            Brand = paymentMethod.Brand,
            LastDigits = paymentMethod.LastDigits,
            Expiry = paymentMethod.Expiry,
            CardholderName = paymentMethod.CardholderName
        };
        return Results.Created($"api/payment-methods/{paymentMethod.Id}", response);
    }
}
