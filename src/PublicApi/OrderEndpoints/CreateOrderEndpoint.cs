using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Places an order from catalog item ids/quantities. The order starts AwaitingPayment;
/// pay it with POST /api/orders/{orderId}/pay.
/// </summary>
public class CreateOrderEndpoint : IEndpoint<IResult, CreateOrderRequest, ClaimsPrincipal, IOrderService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateOrderRequest request, ClaimsPrincipal user, IOrderService orderService) =>
            {
                return await HandleAsync(request, user, orderService);
            })
            .Produces<CreateOrderResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateOrderRequest request, ClaimsPrincipal user, IOrderService orderService)
    {
        var response = new CreateOrderResponse(request.CorrelationId());

        var buyerId = user.Identity?.Name;
        if (string.IsNullOrEmpty(buyerId)) return Results.Unauthorized();

        if (request.Items is null || request.Items.Count == 0)
        {
            return Results.BadRequest("At least one item is required.");
        }

        var address = new Address(
            request.ShippingStreet ?? "123 Main St",
            request.ShippingCity ?? "Redmond",
            request.ShippingState ?? "WA",
            request.ShippingCountry ?? "USA",
            request.ShippingZipCode ?? "98052");

        var items = request.Items.Select(i => new OrderItemRequest(i.CatalogItemId, i.Quantity));
        var order = await orderService.CreateOrderAsync(buyerId, items, address);

        response.OrderId = order.Id;
        response.Status = order.Status.ToString();
        response.Total = order.Total();

        return Results.Created($"api/orders/{order.Id}", response);
    }
}
