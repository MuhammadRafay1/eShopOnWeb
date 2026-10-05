using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.DependencyInjection;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Place and pay for an order from catalog items (Flow 2). Reuses the app's existing Order/OrderItem model.
/// For an accepted investor the paid order's round-up to the next whole euro is set aside. Investing never
/// causes the order to fail — the order is placed and the request succeeds regardless.
/// </summary>
public class PlaceOrderEndpoint : IEndpoint<IResult, PlaceOrderRequest, ClaimsPrincipal>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public PlaceOrderEndpoint(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (PlaceOrderRequest request, ClaimsPrincipal user) => await HandleAsync(request, user))
            .Produces<PlaceOrderResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(PlaceOrderRequest request, ClaimsPrincipal user)
    {
        var shopperId = user.ShopperId();
        if (string.IsNullOrEmpty(shopperId))
            return Results.Unauthorized();

        if (request.Items is null || request.Items.Count == 0)
            return Results.BadRequest(new { error = "At least one order item is required." });

        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var orders = sp.GetRequiredService<IRepository<Order>>();
        var catalogItems = sp.GetRequiredService<IRepository<CatalogItem>>();
        var uriComposer = sp.GetRequiredService<IUriComposer>();
        var investing = sp.GetRequiredService<InvestingService>();
        var logger = sp.GetRequiredService<IAppLogger<PlaceOrderEndpoint>>();

        var ids = request.Items.Select(i => i.CatalogItemId).Distinct().ToArray();
        var items = await catalogItems.ListAsync(new CatalogItemsSpecification(ids), default);

        var orderItems = new List<OrderItem>();
        foreach (var line in request.Items)
        {
            if (line.Quantity <= 0)
                return Results.BadRequest(new { error = $"Quantity for catalog item {line.CatalogItemId} must be positive." });
            var catalogItem = items.FirstOrDefault(c => c.Id == line.CatalogItemId);
            if (catalogItem is null)
                return Results.BadRequest(new { error = $"Catalog item {line.CatalogItemId} was not found." });

            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, uriComposer.ComposePicUri(catalogItem.PictureUri));
            orderItems.Add(new OrderItem(itemOrdered, catalogItem.Price, line.Quantity));
        }

        // eShopOnWeb has no separate payment step: placing the order via this endpoint pays for it.
        var shipToAddress = new Address("Digital delivery", "N/A", "N/A", "N/A", "00000");
        var order = new Order(shopperId, shipToAddress, orderItems);
        await orders.AddAsync(order, default);

        // Set aside the round-up — isolated so nothing about investing can fail the order.
        decimal roundUp = 0m;
        try
        {
            var roundUpCents = await investing.RecordPaidOrderAsync(shopperId, order.Total(), default);
            roundUp = InvestingMappings.ToEuros(roundUpCents);
        }
        catch (System.Exception ex)
        {
            logger.LogWarning($"Setting aside change for order {order.Id} failed and was ignored: {ex.GetType().Name}.");
        }

        return Results.Created($"api/orders/{order.Id}", new PlaceOrderResponse { OrderId = order.Id, RoundUpAmount = roundUp });
    }
}
