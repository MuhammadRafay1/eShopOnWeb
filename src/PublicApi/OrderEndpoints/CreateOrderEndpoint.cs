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
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Places an order from catalog items for the signed-in shopper, reusing the app's existing order model.
/// There is no separate payment step: placing the order marks it paid, which sets aside the order's round-up
/// for an enrolled shopper. Returns the order id and the amount this order set aside.
/// </summary>
public class CreateOrderEndpoint : IEndpoint<IResult, CreateOrderRequest, CreateOrderEndpoint.Dependencies>
{
    public record struct Dependencies(
        ClaimsPrincipal User,
        IRepository<Order> OrderRepository,
        IRepository<CatalogItem> ItemRepository,
        IUriComposer UriComposer,
        IInvestingService InvestingService,
        CancellationToken CancellationToken);

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                CreateOrderRequest request,
                ClaimsPrincipal user,
                IRepository<Order> orderRepository,
                IRepository<CatalogItem> itemRepository,
                IUriComposer uriComposer,
                IInvestingService investingService,
                CancellationToken cancellationToken) =>
            {
                return await HandleAsync(
                    request,
                    new Dependencies(
                        user, orderRepository, itemRepository, uriComposer, investingService, cancellationToken));
            })
            .Produces<CreateOrderResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateOrderRequest request, Dependencies deps)
    {
        var shopperId = deps.User.Identity?.Name;
        if (string.IsNullOrEmpty(shopperId))
        {
            return Results.Unauthorized();
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return Results.BadRequest("An order must contain at least one item.");
        }

        var ids = request.Items.Select(i => i.CatalogItemId).Distinct().ToArray();
        var catalogItems = await deps.ItemRepository.ListAsync(
            new CatalogItemsSpecification(ids), deps.CancellationToken);

        var orderItems = new List<OrderItem>();
        foreach (var line in request.Items)
        {
            var catalogItem = catalogItems.FirstOrDefault(c => c.Id == line.CatalogItemId);
            if (catalogItem is null)
            {
                return Results.BadRequest($"Catalog item {line.CatalogItemId} does not exist.");
            }

            if (line.Quantity <= 0)
            {
                return Results.BadRequest($"Quantity for catalog item {line.CatalogItemId} must be positive.");
            }

            var itemOrdered = new CatalogItemOrdered(
                catalogItem.Id, catalogItem.Name, deps.UriComposer.ComposePicUri(catalogItem.PictureUri));
            orderItems.Add(new OrderItem(itemOrdered, catalogItem.Price, line.Quantity));
        }

        // The app has no separate shipping input on this surface; the order model requires an address.
        var shipToAddress = new Address("N/A", "N/A", "N/A", "N/A", "00000");
        var order = new Order(shopperId, shipToAddress, orderItems);
        await deps.OrderRepository.AddAsync(order, deps.CancellationToken);

        // Set aside this order's round-up and invest when the threshold is reached. This must never make
        // placing the order fail, so the service swallows every investing-related error.
        var roundUp = await deps.InvestingService.ApplyPaidOrderAsync(
            shopperId, order.Total(), deps.CancellationToken);

        return Results.Ok(new CreateOrderResponse
        {
            OrderId = order.Id,
            RoundUpAmount = roundUp
        });
    }
}
