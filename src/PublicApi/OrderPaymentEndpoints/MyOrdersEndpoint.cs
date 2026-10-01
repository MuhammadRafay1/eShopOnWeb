using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class MyOrdersResponse : BaseResponse
{
    public List<OrderStateDto> Orders { get; set; } = new();
}

/// <summary>The caller's own orders with their payment state.</summary>
public class MyOrdersEndpoint : IEndpoint<IResult, string, IOrderPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/my-orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IOrderPaymentService orderPaymentService) =>
            {
                var buyerId = user.FindFirstValue(ClaimTypes.Name)!;
                return await HandleAsync(buyerId, orderPaymentService);
            })
            .Produces<MyOrdersResponse>()
            .WithTags("OrderPaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(string buyerId, IOrderPaymentService orderPaymentService)
    {
        var orders = await orderPaymentService.GetOrdersForBuyerAsync(buyerId, default);
        var response = new MyOrdersResponse { Orders = orders.Select(OrderStateMapper.ToDto).ToList() };
        return Results.Ok(response);
    }
}
