using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

/// <summary>Operator action: cancels before fulfilment, voiding any hold so no money ever moved.</summary>
public class CancelOrderEndpoint : IEndpoint<IResult, CancelOrderRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/cancel",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, IPaymentService paymentService) =>
            {
                return await HandleAsync(new CancelOrderRequest(orderId), paymentService);
            })
            .Produces<CancelOrderResponse>()
            .WithTags("OrderPaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(CancelOrderRequest request, IPaymentService paymentService)
    {
        var outcome = await paymentService.CancelAsync(request.OrderId);
        if (outcome is null)
        {
            return Results.NotFound();
        }

        var response = new CancelOrderResponse(request.CorrelationId())
        {
            OrderId = outcome.OrderId,
            Status = outcome.Status
        };

        return Results.Ok(response);
    }
}
