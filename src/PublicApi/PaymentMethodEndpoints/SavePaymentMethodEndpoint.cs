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
/// Saves a card for the signed-in shopper via PayPal's vault (Vault v3). The response never
/// includes full card details - only a safe descriptor (brand/last4/expiry).
/// </summary>
public class SavePaymentMethodEndpoint : IEndpoint<IResult, SavePaymentMethodRequest, ClaimsPrincipal, ISavedCardService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (SavePaymentMethodRequest request, ClaimsPrincipal user, ISavedCardService savedCardService) =>
            {
                return await HandleAsync(request, user, savedCardService);
            })
            .Produces<SavePaymentMethodResponse>()
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(SavePaymentMethodRequest request, ClaimsPrincipal user, ISavedCardService savedCardService)
    {
        var response = new SavePaymentMethodResponse(request.CorrelationId());

        var buyerId = user.Identity?.Name;
        if (string.IsNullOrEmpty(buyerId)) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.CardNumber) || string.IsNullOrWhiteSpace(request.ExpiryYearMonth))
        {
            return Results.BadRequest("cardNumber and expiryYearMonth are required.");
        }

        var card = new PayPalCardDetails(
            request.CardNumber,
            request.ExpiryYearMonth,
            request.CardholderName,
            request.AddressLine1,
            request.City,
            request.State,
            request.PostalCode,
            request.CountryCode);

        var savedCard = await savedCardService.SaveCardAsync(buyerId, card, request.CorrelationId().ToString());

        response.PaymentMethodId = savedCard.Id;
        response.Description = savedCard.Description;
        response.Brand = savedCard.Brand;
        response.Last4 = savedCard.Last4;
        response.Expiry = savedCard.Expiry;

        return Results.Created($"api/payment-methods/{savedCard.Id}", response);
    }
}
