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

/// <summary>
/// Operator action: marks the order fulfilled and captures (takes) the held funds. Renews a stale
/// authorization first if needed.
/// </summary>
public class FulfilOrderEndpoint : IEndpoint<IResult, int, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/fulfil",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, IPaymentService paymentService) =>
            {
                return await HandleAsync(orderId, paymentService);
            })
            .Produces<FulfilOrderResponse>()
            .WithTags("PaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(int orderId, IPaymentService paymentService)
    {
        var payment = await paymentService.FulfilAsync(orderId, default);

        var response = new FulfilOrderResponse(Guid.NewGuid())
        {
            OrderId = orderId,
            Payment = OrderPaymentDto.From(payment)
        };
        return Results.Ok(response);
    }
}
