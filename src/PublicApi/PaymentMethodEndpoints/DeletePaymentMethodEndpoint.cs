using System;
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

/// <summary>Removes a saved card, both locally and from PayPal's vault. Afterwards it is no longer listed and can no longer be used to pay.</summary>
public class DeletePaymentMethodEndpoint : IEndpoint<IResult, int, string, IPaymentMethodService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/payment-methods/{paymentMethodId}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int paymentMethodId, ClaimsPrincipal user, IPaymentMethodService paymentMethodService) =>
            {
                return await HandleAsync(paymentMethodId, user.Identity!.Name!, paymentMethodService);
            })
            .Produces<DeletePaymentMethodResponse>()
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(int paymentMethodId, string buyerId, IPaymentMethodService paymentMethodService)
    {
        var deleted = await paymentMethodService.DeleteAsync(buyerId, paymentMethodId, default);
        if (!deleted)
        {
            return Results.NotFound();
        }

        return Results.Ok(new DeletePaymentMethodResponse(Guid.NewGuid()) { PaymentMethodId = paymentMethodId });
    }
}
