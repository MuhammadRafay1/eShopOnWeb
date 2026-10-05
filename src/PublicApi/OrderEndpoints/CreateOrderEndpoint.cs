using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.PublicApi.InvestingEndpoints;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Places an order from catalog items for the signed-in shopper, reusing the app's order model (Flow 2).
/// The order is treated as paid on placement; for an enrolled, accepted investor the round-up to the next
/// whole euro is set aside. Investing can never fail order placement.
/// </summary>
public class CreateOrderEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                CreateOrderRequest request,
                HttpContext http,
                IRepository<Order> orderRepository,
                IRepository<CatalogItem> itemRepository,
                IUriComposer uriComposer,
                IInvestingService investing) =>
            {
                var shopperId = http.User.Identity?.Name;
                if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();

                if (request.Items is null || request.Items.Count == 0)
                    return Results.BadRequest(new { error = "At least one order item is required." });
                if (request.Items.Any(i => i.Quantity <= 0))
                    return Results.BadRequest(new { error = "Every item quantity must be greater than zero." });

                var ids = request.Items.Select(i => i.CatalogItemId).Distinct().ToArray();
                var catalogItems = await itemRepository.ListAsync(new CatalogItemsSpecification(ids), http.RequestAborted);
                var byId = catalogItems.ToDictionary(c => c.Id);

                var missing = ids.Where(id => !byId.ContainsKey(id)).ToList();
                if (missing.Count > 0)
                    return Results.BadRequest(new { error = $"Unknown catalog item id(s): {string.Join(", ", missing)}." });

                var orderItems = request.Items.Select(line =>
                {
                    var catalogItem = byId[line.CatalogItemId];
                    var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, uriComposer.ComposePicUri(catalogItem.PictureUri));
                    return new OrderItem(itemOrdered, catalogItem.Price, line.Quantity);
                }).ToList();

                // This flow has no shipping; the order model requires an address, so a placeholder is used.
                var shipToAddress = new Address("N/A", "N/A", "N/A", "N/A", "N/A");
                var order = new Order(shopperId, shipToAddress, orderItems);
                await orderRepository.AddAsync(order, http.RequestAborted);

                // Set aside the round-up. This never throws; the order has already been placed regardless.
                var roundUpInCents = await investing.HandlePaidOrderAsync(shopperId, order.Total(), http.RequestAborted);

                return Results.Created($"api/orders/{order.Id}", new CreateOrderResponse
                {
                    OrderId = order.Id,
                    RoundUpAmount = roundUpInCents / 100m
                });
            })
            .Produces<CreateOrderResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("OrderEndpoints");
    }
}

public class CreateOrderRequest
{
    public List<CreateOrderItem>? Items { get; set; }
}

public class CreateOrderItem
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

public class CreateOrderResponse
{
    public int OrderId { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal RoundUpAmount { get; set; }
}
