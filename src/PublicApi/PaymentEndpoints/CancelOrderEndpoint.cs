using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

/// <summary>Operator action: cancels an order before fulfilment, releasing the held funds. No money ever moves.</summary>
public class CancelOrderEndpoint : IEndpoint<IResult, int, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/cancel",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, IPaymentService paymentService) =>
            {
                return await HandleAsync(orderId, paymentService);
            })
            .Produces<CancelOrderResponse>()
            .WithTags("PaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(int orderId, IPaymentService paymentService)
    {
        var payment = await paymentService.CancelAsync(orderId, default);

        var response = new CancelOrderResponse(Guid.NewGuid())
        {
            OrderId = orderId,
            Payment = OrderPaymentDto.From(payment)
        };
        return Results.Ok(response);
    }
}
