using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.PublicApi.PaymentModels;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// POST api/orders/{orderId}/pay — authorize the order total (place a hold; do not capture). The body
/// carries either one-off card details or the caller's own saved paymentMethodId. Shopper-scoped: acts
/// only on the caller's own order and saved card.
/// </summary>
public class PayOrderEndpoint : IEndpoint<IResult, PayOrderRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/pay",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (int orderId, PayOrderRequest request, ClaimsPrincipal user, IPaymentService service, CancellationToken ct) =>
            {
                request.OrderId = orderId;
                request.BuyerId = user.Identity!.Name!;
                return await Run(request, service, ct);
            })
            .Produces<OrderPaymentView>()
            .WithTags("OrderEndpoints");
    }

    public Task<IResult> HandleAsync(PayOrderRequest request, IPaymentService service) =>
        Run(request, service, CancellationToken.None);

    private static async Task<IResult> Run(PayOrderRequest request, IPaymentService service, CancellationToken ct)
    {
        var command = new PayCommand
        {
            Card = request.Card?.ToCardDetails(),
            PaymentMethodId = request.PaymentMethodId
        };
        var view = await service.PayAsync(request.BuyerId, request.OrderId, command, ct);
        return Results.Ok(view);
    }
}
