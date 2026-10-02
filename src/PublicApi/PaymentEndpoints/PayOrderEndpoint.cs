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

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

/// <summary>POST /api/orders/{orderId}/pay — authorizes (holds) the order total; does not take the money yet.</summary>
public class PayOrderEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId:int}/pay",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                int orderId,
                PayOrderRequest request,
                ClaimsPrincipal user,
                IOrderPaymentService service,
                CancellationToken cancellationToken) =>
            {
                var buyerId = PaymentUser.BuyerId(user);
                var input = new PayInput
                {
                    Card = request.Card?.ToInput(),
                    SavedPaymentMethodId = request.SavedPaymentMethodId
                };
                var view = await service.PayAsync(buyerId, orderId, input, cancellationToken);
                return Results.Ok(view);
            })
            .WithTags("PaymentEndpoints");
    }
}
