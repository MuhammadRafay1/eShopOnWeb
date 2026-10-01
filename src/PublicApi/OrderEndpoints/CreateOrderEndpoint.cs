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
/// Places an order from catalog item ids and quantities. The caller's identity (from the JWT) becomes
/// the order's buyer. Prices come from the catalog, never the request. The order starts AwaitingPayment -
/// pay it with POST /api/orders/{orderId}/pay.
/// </summary>
public class CreateOrderEndpoint : IEndpoint<IResult, CreateOrderRequest, string, IOrderPlacementService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateOrderRequest request, ClaimsPrincipal user, IOrderPlacementService orderPlacementService) =>
            {
                return await HandleAsync(request, user.Identity!.Name!, orderPlacementService);
            })
            .Produces<CreateOrderResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateOrderRequest request, string buyerId, IOrderPlacementService orderPlacementService)
    {
        var response = new CreateOrderResponse(request.CorrelationId());

        var items = request.Items
            .Select(i => new OrderItemInput(i.CatalogItemId, i.Quantity))
            .ToList();

        Address? shipToAddress = null;
        if (request is { ShipToStreet: not null, ShipToCity: not null, ShipToState: not null, ShipToCountry: not null, ShipToZipCode: not null })
        {
            shipToAddress = new Address(request.ShipToStreet, request.ShipToCity, request.ShipToState, request.ShipToCountry, request.ShipToZipCode);
        }

        var order = await orderPlacementService.PlaceOrderAsync(buyerId, items, shipToAddress, default);

        response.OrderId = order.Id;
        response.Total = order.Total();
        response.OrderDate = order.OrderDate;
        return Results.Created($"api/my-orders#{order.Id}", response);
    }
}
