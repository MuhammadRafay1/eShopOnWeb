using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

/// <summary>Operator action: captures the authorized payment (renewing it first if it has gone stale).</summary>
public class FulfilOrderEndpoint : IEndpoint<IResult, int, IOrderPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/fulfil",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, IOrderPaymentService orderPaymentService) =>
            {
                return await HandleAsync(orderId, orderPaymentService);
            })
            .Produces<OrderStateDto>()
            .WithTags("OrderPaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(int orderId, IOrderPaymentService orderPaymentService)
    {
        var view = await orderPaymentService.FulfilAsync(orderId, default);
        return Results.Ok(OrderStateMapper.ToDto(view));
    }
}
