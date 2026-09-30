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
using Microsoft.eShopWeb.ApplicationCore.Models.Orders;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

/// <summary>
/// Places an order directly from catalog item ids/quantities (no basket involved), reusing the
/// existing Order/OrderItem model, and opens the payment that a later /pay call will authorize.
/// </summary>
public class CreateOrderEndpoint : IEndpoint<IResult, CreateOrderRequest, ClaimsPrincipal, IOrderPlacementService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateOrderRequest request, ClaimsPrincipal user, IOrderPlacementService orderPlacementService) =>
            {
                return await HandleAsync(request, user, orderPlacementService);
            })
            .Produces<CreateOrderResponse>(StatusCodes.Status201Created)
            .WithTags("OrderPaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateOrderRequest request, ClaimsPrincipal user, IOrderPlacementService orderPlacementService)
    {
        var buyerId = user.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        var items = request.Items
            .Select(i => new OrderPlacementItem { CatalogItemId = i.CatalogItemId, Quantity = i.Quantity })
            .ToList();

        Address? shipToAddress = request.ShipToAddress is null
            ? null
            : new Address(request.ShipToAddress.Street, request.ShipToAddress.City, request.ShipToAddress.State, request.ShipToAddress.Country, request.ShipToAddress.ZipCode);

        var result = await orderPlacementService.PlaceOrderAsync(buyerId, items, shipToAddress);

        var response = new CreateOrderResponse(request.CorrelationId())
        {
            OrderId = result.OrderId,
            Status = "AwaitingPayment",
            Total = result.Total,
            Currency = result.CurrencyCode
        };

        return Results.Created($"api/orders/{result.OrderId}", response);
    }
}
