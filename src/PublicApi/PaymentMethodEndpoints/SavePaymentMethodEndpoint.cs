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

/// <summary>Saves a card for the signed-in shopper. Full card details are never stored or echoed back — only a safe descriptor.</summary>
public class SavePaymentMethodEndpoint : IEndpoint<IResult, SavePaymentMethodRequest, ClaimsPrincipal, IPaymentMethodService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (SavePaymentMethodRequest request, ClaimsPrincipal user, IPaymentMethodService paymentMethodService) =>
            {
                return await HandleAsync(request, user, paymentMethodService);
            })
            .Produces<SavePaymentMethodResponse>(StatusCodes.Status201Created)
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(SavePaymentMethodRequest request, ClaimsPrincipal user, IPaymentMethodService paymentMethodService)
    {
        var buyerId = user.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        var summary = await paymentMethodService.SaveAsync(buyerId, request.Card);

        var response = new SavePaymentMethodResponse(request.CorrelationId())
        {
            PaymentMethodId = summary.PaymentMethodId,
            Brand = summary.Brand,
            LastDigits = summary.LastDigits,
            Expiry = summary.Expiry,
            CardholderName = summary.CardholderName
        };

        return Results.Created($"api/payment-methods/{summary.PaymentMethodId}", response);
    }
}
