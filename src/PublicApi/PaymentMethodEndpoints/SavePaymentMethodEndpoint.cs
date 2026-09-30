using System.Security.Claims;
using System.Threading;
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
/// Saves a card to the shopper's vault for reuse on later orders. Never stores or returns the full PAN.
/// </summary>
public class SavePaymentMethodEndpoint : IEndpoint<IResult, SavePaymentMethodRequest, ISavedCardService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (SavePaymentMethodRequest request, ClaimsPrincipal user, ISavedCardService savedCardService) =>
            {
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, savedCardService);
            })
            .Produces<SavePaymentMethodResponse>()
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(SavePaymentMethodRequest request, ISavedCardService savedCardService)
    {
        var savedCard = await savedCardService.SaveCardAsync(request.BuyerId, request.Card.ToCardInput(), CancellationToken.None);

        var response = new SavePaymentMethodResponse(request.CorrelationId())
        {
            PaymentMethodId = savedCard.Id,
            Brand = savedCard.Brand,
            LastFourDigits = savedCard.LastFourDigits,
            Expiry = savedCard.Expiry,
            CardholderName = savedCard.CardholderName
        };

        return Results.Created($"api/payment-methods/{savedCard.Id}", response);
    }
}
