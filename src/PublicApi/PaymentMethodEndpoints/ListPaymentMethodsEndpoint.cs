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

/// <summary>The caller's saved cards, described safely (brand/last4/expiry) - never full details.</summary>
public class ListPaymentMethodsEndpoint : IEndpoint<IResult, ListPaymentMethodsRequest, ClaimsPrincipal, ISavedCardService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, ISavedCardService savedCardService) =>
            {
                return await HandleAsync(new ListPaymentMethodsRequest(), user, savedCardService);
            })
            .Produces<ListPaymentMethodsResponse>()
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(ListPaymentMethodsRequest request, ClaimsPrincipal user, ISavedCardService savedCardService)
    {
        var response = new ListPaymentMethodsResponse(request.CorrelationId());

        var buyerId = user.Identity?.Name;
        if (string.IsNullOrEmpty(buyerId)) return Results.Unauthorized();

        var cards = await savedCardService.ListCardsAsync(buyerId);

        response.PaymentMethods = cards.Select(c => new PaymentMethodDto
        {
            PaymentMethodId = c.Id,
            Description = c.Description,
            Brand = c.Brand,
            Last4 = c.Last4,
            Expiry = c.Expiry,
            CreatedAt = c.CreatedAt
        }).ToList();

        return Results.Ok(response);
    }
}
