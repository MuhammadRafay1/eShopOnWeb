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
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.DependencyInjection;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Places an order from catalog items for the signed-in shopper, reusing the app's order model.
/// There is no separate payment step: a placed order is treated as paid, so for an accepted
/// investor its round-up is set aside. Investing never causes order placement to fail.
/// </summary>
public class PlaceOrderEndpoint : IEndpoint<IResult, PlaceOrderRequest, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (PlaceOrderRequest request, HttpContext http) => await HandleAsync(request, http))
            .Produces<PlaceOrderResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(PlaceOrderRequest request, HttpContext http)
    {
        var shopper = InvestingContractMapping.ResolveShopper(http);
        if (string.IsNullOrEmpty(shopper))
            return Results.Unauthorized();

        if (request.Items is null || request.Items.Count == 0)
            return Results.BadRequest(new { error = "At least one order item is required." });
        if (request.Items.Any(i => i.Quantity <= 0))
            return Results.BadRequest(new { error = "Every item quantity must be greater than zero." });

        var services = http.RequestServices;
        var catalog = services.GetRequiredService<IRepository<CatalogItem>>();
        var orders = services.GetRequiredService<IRepository<Order>>();
        var investing = services.GetRequiredService<IInvestingService>();
        var uriComposer = services.GetRequiredService<IUriComposer>();

        var itemIds = request.Items.Select(i => i.CatalogItemId).Distinct().ToArray();
        var catalogItems = await catalog.ListAsync(new CatalogItemsSpecification(itemIds), http.RequestAborted);
        if (catalogItems.Count != itemIds.Length)
            return Results.BadRequest(new { error = "One or more catalog items do not exist." });

        var catalogById = catalogItems.ToDictionary(c => c.Id);
        var orderItems = new List<OrderItem>();
        foreach (var line in request.Items)
        {
            var catalogItem = catalogById[line.CatalogItemId];
            var pictureUri = uriComposer.ComposePicUri(string.IsNullOrEmpty(catalogItem.PictureUri) ? "eCatalog-item-default.png" : catalogItem.PictureUri);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, pictureUri);
            orderItems.Add(new OrderItem(itemOrdered, catalogItem.Price, line.Quantity));
        }

        // No shipping is collected on this API; use a sentinel address for the reused order model.
        var shipToAddress = new Address("Not provided", "N/A", "N/A", "N/A", "00000");
        var order = new Order(shopper, shipToAddress, orderItems);
        await orders.AddAsync(order, http.RequestAborted);

        // The order is placed and the request succeeds regardless of what happens next.
        var roundUp = await investing.HandlePaidOrderAsync(shopper, order.Total(), http.RequestAborted);

        return Results.Ok(new PlaceOrderResponse { OrderId = order.Id, RoundUpAmount = roundUp });
    }
}
