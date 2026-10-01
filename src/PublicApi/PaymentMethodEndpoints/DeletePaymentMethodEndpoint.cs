using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

/// <summary>Removes a saved card for the signed-in shopper. Afterwards it can no longer be listed or used to pay.</summary>
public class DeletePaymentMethodEndpoint : IEndpoint<IResult, string, string, IPaymentMethodService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/payment-methods/{paymentMethodId}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string paymentMethodId, ClaimsPrincipal user, IPaymentMethodService paymentMethodService) =>
            {
                var buyerId = user.FindFirstValue(ClaimTypes.Name)!;
                return await HandleAsync(paymentMethodId, buyerId, paymentMethodService);
            })
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(string paymentMethodId, string buyerId, IPaymentMethodService paymentMethodService)
    {
        await paymentMethodService.DeleteAsync(buyerId, paymentMethodId, default);
        return Results.NoContent();
    }
}
