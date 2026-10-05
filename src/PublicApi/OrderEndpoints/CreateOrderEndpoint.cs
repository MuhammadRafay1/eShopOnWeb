using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
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

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Places an order from catalog items for the signed-in shopper, reusing the app's existing
/// order/order-item model. eShopOnWeb has no separate payment step, so the order is paid on
/// placement; for an accepted investor the change is then set aside (rounded up to the next whole
/// euro). Setting aside the change never causes this request to fail.
/// </summary>
public class CreateOrderEndpoint : IEndpoint
{
    private const string PlaceholderAddressValue = "N/A";

    private readonly IUriComposer _uriComposer;

    public CreateOrderEndpoint(IUriComposer uriComposer)
    {
        _uriComposer = uriComposer;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                CreateOrderRequest request,
                ClaimsPrincipal user,
                IRepository<Order> orderRepository,
                IRepository<CatalogItem> itemRepository,
                IInvestingProcessor investing) =>
                await HandleAsync(request, user, orderRepository, itemRepository, investing))
            .Produces<CreateOrderResponse>()
            .WithTags("OrderEndpoints");
    }

    private async Task<IResult> HandleAsync(
        CreateOrderRequest request,
        ClaimsPrincipal user,
        IRepository<Order> orderRepository,
        IRepository<CatalogItem> itemRepository,
        IInvestingProcessor investing)
    {
        var buyerId = user.Identity?.Name;
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));

        if (request.Items is null || request.Items.Count == 0)
        {
            return Results.BadRequest("At least one order item is required.");
        }
        if (request.Items.Any(i => i.Quantity <= 0))
        {
            return Results.BadRequest("Every order item must have a quantity greater than zero.");
        }

        var ids = request.Items.Select(i => i.CatalogItemId).Distinct().ToArray();
        var catalogItems = await itemRepository.ListAsync(new CatalogItemsSpecification(ids));
        var missing = ids.Where(id => catalogItems.All(c => c.Id != id)).ToList();
        if (missing.Count != 0)
        {
            return Results.BadRequest($"Unknown catalog item id(s): {string.Join(", ", missing)}.");
        }

        var orderItems = request.Items.Select(item =>
        {
            var catalogItem = catalogItems.First(c => c.Id == item.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            return new OrderItem(itemOrdered, catalogItem.Price, item.Quantity);
        }).ToList();

        // No shipping address is collected by this API; the order model requires one, so store a
        // placeholder. The investing capability only ever uses the order total, not the address.
        var address = new Address(PlaceholderAddressValue, PlaceholderAddressValue, string.Empty, PlaceholderAddressValue, PlaceholderAddressValue);
        var order = new Order(buyerId, address, orderItems);
        order = await orderRepository.AddAsync(order);

        var response = new CreateOrderResponse(request.CorrelationId())
        {
            OrderId = order.Id,
            RoundUpAmount = await SetAsideSafelyAsync(investing, buyerId!, order.Id, order.Total())
        };
        return Results.Ok(response);
    }

    private static async Task<decimal> SetAsideSafelyAsync(IInvestingProcessor investing, string buyerId, int orderId, decimal total)
    {
        try
        {
            // The order has been placed and (in this app) paid; set aside its change.
            return await investing.SetAsideForPaidOrderAsync(buyerId, orderId, total);
        }
        catch
        {
            // Placing the order must never fail because of anything to do with investing.
            return 0m;
        }
    }
}
