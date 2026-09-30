using System.Linq;
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
/// The signed-in shopper's own saved cards. Never returns another shopper's cards.
/// </summary>
public class ListPaymentMethodsEndpoint : IEndpoint<IResult, string, ISavedCardService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ClaimsPrincipal user, ISavedCardService savedCardService) =>
            {
                return await HandleAsync(user.Identity!.Name!, savedCardService);
            })
            .Produces<SavePaymentMethodResponse[]>()
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(string buyerId, ISavedCardService savedCardService)
    {
        var cards = await savedCardService.ListCardsAsync(buyerId);
        var response = cards.Select(c => new SavePaymentMethodResponse
        {
            PaymentMethodId = c.PaymentMethodId,
            Brand = c.Brand,
            Last4 = c.Last4,
            Expiry = c.Expiry,
            Type = c.CardType,
            Label = c.Label
        }).ToArray();
        return Results.Ok(response);
    }
}
