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
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class PlaceOrderItemRequest
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

public class ShipToAddressRequest
{
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? ZipCode { get; set; }
}

public class PlaceOrderRequest
{
    public List<PlaceOrderItemRequest> Items { get; set; } = new();
    public ShipToAddressRequest? ShipToAddress { get; set; }

    /// <summary>Set from the caller's JWT after model binding - never accepted from the request body.</summary>
    public string BuyerId { get; set; } = string.Empty;
}

public class PlaceOrderResponse
{
    public int OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string Currency { get; set; } = string.Empty;
}

/// <summary>
/// Places an order directly from catalog item ids/quantities - reusing the Order/OrderItem
/// aggregate the storefront already uses - so a payment can be driven end to end through
/// PublicApi alone. The order starts out awaiting payment.
/// </summary>
public class PlaceOrderEndpoint : IEndpoint<IResult, PlaceOrderRequest, OrderEndpointServices>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (PlaceOrderRequest request, ClaimsPrincipal user, OrderEndpointServices services) =>
            {
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, services);
            })
            .Produces<PlaceOrderResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(PlaceOrderRequest request, OrderEndpointServices services)
    {
        if (request.Items is null || request.Items.Count == 0)
            return Results.BadRequest("At least one item is required.");

        if (request.Items.Any(i => i.Quantity < 1))
            return Results.BadRequest("Every item quantity must be at least 1.");

        var catalogItemIds = request.Items.Select(i => i.CatalogItemId).Distinct().ToArray();
        var catalogItems = await services.CatalogItemRepository.ListAsync(new CatalogItemsSpecification(catalogItemIds));
        if (catalogItems.Count != catalogItemIds.Length)
        {
            var missing = catalogItemIds.Except(catalogItems.Select(c => c.Id));
            return Results.BadRequest($"Unknown catalog item id(s): {string.Join(", ", missing)}");
        }

        var orderItems = request.Items.Select(requested =>
        {
            var catalogItem = catalogItems.First(c => c.Id == requested.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, services.UriComposer.ComposePicUri(catalogItem.PictureUri));
            return new OrderItem(itemOrdered, catalogItem.Price, requested.Quantity);
        }).ToList();

        var address = request.ShipToAddress is { } a
            ? new Address(a.Street ?? "Unknown", a.City ?? "Unknown", a.State ?? "Unknown", a.Country ?? "Unknown", a.ZipCode ?? "00000")
            : new Address("1 Microsoft Way", "Redmond", "WA", "USA", "98052");

        var order = new Order(request.BuyerId, address, orderItems);
        await services.OrderRepository.AddAsync(order);

        return Results.Created($"api/orders/{order.Id}", new PlaceOrderResponse
        {
            OrderId = order.Id,
            Status = order.Status.ToString(),
            Total = order.Total(),
            Currency = services.PaymentGateway.Currency
        });
    }
}
