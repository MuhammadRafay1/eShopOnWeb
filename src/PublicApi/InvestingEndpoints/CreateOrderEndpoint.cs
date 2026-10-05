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
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Places an order from catalog items, reusing the app's existing Order/OrderItem model. For an enrolled
/// shopper the order is treated as paid on creation and the round-up is set aside. Placing the order never
/// fails for an investing reason.
/// </summary>
public class CreateOrderEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                CreateOrderRequest request,
                ClaimsPrincipal user,
                IRepository<Order> orderRepository,
                IRepository<CatalogItem> itemRepository,
                IInvestingService investingService,
                IUriComposer uriComposer,
                CancellationToken cancellationToken) =>
            {
                var buyerId = CallerIdentity.GetBuyerId(user);
                if (string.IsNullOrEmpty(buyerId))
                {
                    return Results.Unauthorized();
                }

                if (request.Items is null || request.Items.Count == 0)
                {
                    return Results.BadRequest("At least one order item is required.");
                }
                if (request.Items.Any(i => i.Quantity <= 0))
                {
                    return Results.BadRequest("Each order item quantity must be greater than zero.");
                }

                var ids = request.Items.Select(i => i.CatalogItemId).Distinct().ToArray();
                var catalogItems = await itemRepository.ListAsync(new CatalogItemsSpecification(ids), cancellationToken);
                var missing = ids.Where(id => catalogItems.All(c => c.Id != id)).ToArray();
                if (missing.Length > 0)
                {
                    return Results.BadRequest($"Unknown catalog item id(s): {string.Join(", ", missing)}.");
                }

                var items = request.Items.Select(line =>
                {
                    var catalogItem = catalogItems.First(c => c.Id == line.CatalogItemId);
                    var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, uriComposer.ComposePicUri(catalogItem.PictureUri));
                    return new OrderItem(itemOrdered, catalogItem.Price, line.Quantity);
                }).ToList();

                // No separate shipping step on this API surface; use a placeholder address for the order record.
                var address = new Address("N/A", "N/A", "N/A", "N/A", "00000");
                var order = new Order(buyerId, address, items);
                await orderRepository.AddAsync(order, cancellationToken);

                // The order is now paid. Setting aside the change must never make this fail.
                var roundUpCents = await investingService.HandleOrderPaidAsync(buyerId, order.Total(), cancellationToken);

                return Results.Ok(new CreateOrderResponse
                {
                    OrderId = order.Id,
                    RoundUpAmount = Money.ToEuros(roundUpCents)
                });
            })
            .Produces<CreateOrderResponse>()
            .WithTags("OrderEndpoints");
    }
}
