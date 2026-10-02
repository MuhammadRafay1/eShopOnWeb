using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.PublicApi.PaymentEndpoints;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

/// <summary>GET /api/payment-methods — the caller's saved cards.</summary>
public class ListPaymentMethodsEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                ClaimsPrincipal user,
                ISavedCardService service,
                CancellationToken cancellationToken) =>
            {
                var buyerId = PaymentUser.BuyerId(user);
                var paymentMethods = await service.GetCardsAsync(buyerId, cancellationToken);
                return Results.Ok(new { paymentMethods });
            })
            .WithTags("PaymentMethodEndpoints");
    }
}
