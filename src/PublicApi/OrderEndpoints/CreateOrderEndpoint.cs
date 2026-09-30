using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Places an order from catalog item ids and quantities. The caller's identity comes from the
/// token; prices come from the catalog. The order starts awaiting payment.
/// </summary>
public class CreateOrderEndpoint : IEndpoint<IResult, CreateOrderRequest, IOrderService>
{
    private readonly PayPalOptions _options;

    public CreateOrderEndpoint(PayPalOptions options)
    {
        _options = options;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateOrderRequest request, ClaimsPrincipal user, IOrderService orderService) =>
            {
                request.BuyerId = CallerIdentity.GetBuyerId(user);
                return await HandleAsync(request, orderService);
            })
            .Produces<CreateOrderResponse>(StatusCodes.Status201Created)
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateOrderRequest request, IOrderService orderService)
    {
        if (string.IsNullOrEmpty(request.BuyerId))
            return Results.Unauthorized();
        if (request.Items is null || request.Items.Count == 0)
            return Results.BadRequest("At least one order item is required.");

        var shipTo = new Address(
            request.ShipToAddress?.Street ?? string.Empty,
            request.ShipToAddress?.City ?? string.Empty,
            request.ShipToAddress?.State ?? string.Empty,
            request.ShipToAddress?.Country ?? string.Empty,
            request.ShipToAddress?.ZipCode ?? string.Empty);

        var items = request.Items.Select(i => new OrderItemRequest(i.CatalogItemId, i.Quantity));
        var order = await orderService.CreateOrderFromItemsAsync(request.BuyerId, shipTo, items);

        var response = new CreateOrderResponse(request.CorrelationId())
        {
            OrderId = order.Id,
            Status = order.Status.ToString(),
            Total = order.Total(),
            Currency = _options.Currency
        };
        return Results.Created($"api/orders/{order.Id}", response);
    }
}
