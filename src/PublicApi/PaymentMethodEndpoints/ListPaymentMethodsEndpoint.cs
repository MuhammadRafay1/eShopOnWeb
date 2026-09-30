using System.Linq;
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
/// Lists the caller's own saved cards.
/// </summary>
public class ListPaymentMethodsEndpoint : IEndpoint<IResult, ListPaymentMethodsRequest, ISavedCardService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, ISavedCardService savedCardService) =>
                await HandleAsync(new ListPaymentMethodsRequest(user.Identity!.Name!), savedCardService))
            .Produces<ListPaymentMethodsResponse>()
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(ListPaymentMethodsRequest request, ISavedCardService savedCardService)
    {
        var response = new ListPaymentMethodsResponse(request.CorrelationId());

        var cards = await savedCardService.GetCardsForBuyerAsync(request.BuyerId, CancellationToken.None);
        response.PaymentMethods = cards.Select(c => new PaymentMethodDto
        {
            PaymentMethodId = c.Id,
            Brand = c.Brand,
            LastFourDigits = c.LastFourDigits,
            Expiry = c.Expiry,
            CardholderName = c.CardholderName
        }).ToList();

        return Results.Ok(response);
    }
}
