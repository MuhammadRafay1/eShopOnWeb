using System.Collections.Generic;
using System.Linq;
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
using Microsoft.Extensions.DependencyInjection;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// POST /api/orders — place an order from catalog items for the signed-in shopper, reusing the app's
/// existing order/order-item model. There is no separate payment step: placing the order is the point
/// it becomes paid, so any round-up is set aside for an enrolled investor here. Placing the order never
/// fails because of investing.
/// </summary>
public class CreateOrderEndpoint : IEndpoint<IResult, CreateOrderRequest, HttpContext>
{
    // There is no shipping step on this API; orders carry a placeholder address to satisfy the model.
    private static Address PlaceholderAddress() => new("N/A", "N/A", "N/A", "N/A", "00000");

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateOrderRequest request, HttpContext http) => await HandleAsync(request, http))
            .Produces<CreateOrderResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateOrderRequest request, HttpContext http)
    {
        var buyerId = InvestingEndpointHelpers.BuyerId(http.User);
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        if (request?.Items is null || request.Items.Count == 0 || request.Items.Any(i => i.Quantity <= 0))
        {
            return Results.BadRequest(new { message = "items are required, each with a catalogItemId and a quantity of at least 1." });
        }

        var services = http.RequestServices;
        var itemRepository = services.GetRequiredService<IRepository<CatalogItem>>();
        var orderRepository = services.GetRequiredService<IRepository<Order>>();
        var uriComposer = services.GetRequiredService<IUriComposer>();
        var investing = services.GetRequiredService<IInvestingService>();

        var ids = request.Items.Select(i => i.CatalogItemId).Distinct().ToArray();
        var catalogItems = await itemRepository.ListAsync(new CatalogItemsSpecification(ids), http.RequestAborted);
        var byId = catalogItems.ToDictionary(c => c.Id);

        var missing = ids.Where(id => !byId.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
        {
            return Results.BadRequest(new { message = $"Unknown catalog item id(s): {string.Join(", ", missing)}." });
        }

        var orderItems = new List<OrderItem>();
        foreach (var line in request.Items)
        {
            var catalogItem = byId[line.CatalogItemId];
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, uriComposer.ComposePicUri(catalogItem.PictureUri));
            orderItems.Add(new OrderItem(itemOrdered, catalogItem.Price, line.Quantity));
        }

        var order = new Order(buyerId, PlaceholderAddress(), orderItems);
        var created = await orderRepository.AddAsync(order, http.RequestAborted);

        // The order is placed and paid; set aside its round-up. This never throws.
        var roundUp = await investing.RecordPaidOrderAsync(buyerId, created.Total(), http.RequestAborted);

        return Results.Created($"api/orders/{created.Id}",
            new CreateOrderResponse { OrderId = created.Id, RoundUpAmount = roundUp });
    }
}
