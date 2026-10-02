using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.PublicApi.PaymentEndpoints;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

/// <summary>POST /api/payment-methods — saves a card for the signed-in shopper.</summary>
public class SavePaymentMethodEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                SavePaymentMethodRequest request,
                ClaimsPrincipal user,
                ISavedCardService service,
                CancellationToken cancellationToken) =>
            {
                var buyerId = PaymentUser.BuyerId(user);
                if (request.Card is null)
                {
                    throw new PaymentValidationException("Card details are required.");
                }

                var view = await service.SaveCardAsync(buyerId, request.Card.ToInput(), cancellationToken);
                return Results.Created($"/api/payment-methods/{view.PaymentMethodId}",
                    new { paymentMethodId = view.PaymentMethodId, paymentMethod = view });
            })
            .WithTags("PaymentMethodEndpoints");
    }
}
