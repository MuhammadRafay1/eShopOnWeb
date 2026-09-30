using System.Collections.Generic;
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

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class OrderItemRequest
{
    public int CatalogItemId { get; init; }
    public int Quantity { get; init; }
}

public class ShipToAddressRequest
{
    public string Street { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string ZipCode { get; init; } = string.Empty;
}

public class PlaceOrderRequest
{
    public List<OrderItemRequest> Items { get; init; } = new();
    public ShipToAddressRequest? ShipToAddress { get; init; }
}

public class PlaceOrderResponse
{
    public int OrderId { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal Total { get; init; }
    public string Currency { get; init; } = string.Empty;
}

/// <summary>
/// Places an order from catalog item ids/quantities on behalf of the signed-in shopper. Starts
/// the order in AwaitingPayment; pay it with POST /api/orders/{orderId}/pay.
/// </summary>
public class PlaceOrderEndpoint : IEndpoint<IResult, PlaceOrderRequest, string, IPaymentOrderService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (PlaceOrderRequest request, ClaimsPrincipal user, IPaymentOrderService orderService) =>
            {
                return await HandleAsync(request, user.Identity!.Name!, orderService);
            })
            .Produces<PlaceOrderResponse>(StatusCodes.Status201Created)
            .WithTags("OrderPaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(PlaceOrderRequest request, string buyerId, IPaymentOrderService orderService)
    {
        var items = request.Items.Select(i => (i.CatalogItemId, i.Quantity));
        var shipTo = request.ShipToAddress is null
            ? null
            : new Address(request.ShipToAddress.Street, request.ShipToAddress.City, request.ShipToAddress.State, request.ShipToAddress.Country, request.ShipToAddress.ZipCode);

        var result = await orderService.PlaceOrderAsync(buyerId, items, shipTo);

        var response = new PlaceOrderResponse
        {
            OrderId = result.OrderId,
            Status = result.Status,
            Total = result.Total,
            Currency = result.Currency
        };
        return Results.Created($"api/orders/{response.OrderId}", response);
    }
}
