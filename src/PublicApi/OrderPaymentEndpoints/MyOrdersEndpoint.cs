using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class MyOrderResponse
{
    public int OrderId { get; init; }
    public DateTimeOffset OrderDate { get; init; }
    public PaymentActionResponse Payment { get; init; } = null!;
}

/// <summary>
/// The signed-in shopper's own orders and their payment state. Never returns another shopper's
/// orders.
/// </summary>
public class MyOrdersEndpoint : IEndpoint<IResult, string, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/my-orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ClaimsPrincipal user, IPaymentService paymentService) =>
            {
                return await HandleAsync(user.Identity!.Name!, paymentService);
            })
            .Produces<MyOrderResponse[]>()
            .WithTags("OrderPaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(string buyerId, IPaymentService paymentService)
    {
        var orders = await paymentService.GetOrdersForBuyerAsync(buyerId);
        var response = orders
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new MyOrderResponse { OrderId = o.OrderId, OrderDate = o.OrderDate, Payment = PaymentActionResponse.From(o.Payment) })
            .ToArray();
        return Results.Ok(response);
    }
}
