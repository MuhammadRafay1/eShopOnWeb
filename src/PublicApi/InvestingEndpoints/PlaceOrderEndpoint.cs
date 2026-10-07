using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// POST /api/orders — place an order from catalog items for the signed-in shopper, reusing
/// the app's existing order/order-item model. The order is treated as paid on placement (the
/// app has no separate payment step), so this is where an accepted investor's round-up is set
/// aside. Returns the order id and the amount this order set aside.
/// </summary>
public class PlaceOrderEndpoint : IEndpoint
{
    // A placed-through-the-API order still needs a ship-to address (the order model requires
    // one); the storefront is not involved here, so a neutral placeholder is used.
    private static readonly Address PlaceholderShipTo = new("N/A", "N/A", "N/A", "N/A", "00000");

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (PlaceOrderRequest request, ClaimsPrincipal user,
                   IRepository<Order> orderRepository, IReadRepository<CatalogItem> itemRepository,
                   IUriComposer uriComposer, IInvestingService investingService, CancellationToken ct) =>
            {
                var buyerId = Caller.BuyerId(user);
                if (string.IsNullOrEmpty(buyerId))
                {
                    return Results.Unauthorized();
                }

                if (request.Items is null || request.Items.Count == 0)
                {
                    return Results.BadRequest(new { error = "At least one order item is required." });
                }
                if (request.Items.Any(i => i.Quantity <= 0))
                {
                    return Results.BadRequest(new { error = "Every item quantity must be greater than zero." });
                }

                var ids = request.Items.Select(i => i.CatalogItemId).Distinct().ToArray();
                var catalogItems = await itemRepository.ListAsync(new CatalogItemsSpecification(ids), ct);
                var missing = ids.Where(id => catalogItems.All(c => c.Id != id)).ToArray();
                if (missing.Length > 0)
                {
                    return Results.BadRequest(new { error = $"Unknown catalog item(s): {string.Join(", ", missing)}." });
                }

                var orderItems = request.Items.Select(line =>
                {
                    var catalogItem = catalogItems.First(c => c.Id == line.CatalogItemId);
                    var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, uriComposer.ComposePicUri(catalogItem.PictureUri));
                    return new OrderItem(itemOrdered, catalogItem.Price, line.Quantity);
                }).ToList();

                var order = new Order(buyerId, PlaceholderShipTo, orderItems);
                await orderRepository.AddAsync(order, ct);

                // The order is now placed and paid. Set the shopper's change aside — this never
                // throws and never affects the placed order.
                var roundUp = await investingService.HandlePaidOrderAsync(buyerId, order.Id, order.Total(), ct);

                return Results.Created($"api/orders/{order.Id}",
                    new PlaceOrderResponse { OrderId = order.Id, RoundUpAmount = roundUp });
            })
            .Produces<PlaceOrderResponse>(StatusCodes.Status201Created)
            .WithTags("InvestingEndpoints");
    }
}
