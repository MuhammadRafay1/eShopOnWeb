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
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Place an order from catalog items, reusing the app's existing order/order-item model. The
/// caller's identity comes from the token. eShopOnWeb has no separate payment step, so a placed
/// order is treated as paid: for an accepted investor its round-up is set aside straight away.
/// Placing an order never fails for any investing reason.
/// </summary>
public class PlaceOrderEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (PlaceOrderRequest request,
                   ClaimsPrincipal user,
                   IRepository<Order> orderRepository,
                   IReadRepository<CatalogItem> catalogRepository,
                   IInvestingService investing,
                   IAppLogger<PlaceOrderEndpoint> logger,
                   CancellationToken cancellationToken) =>
            {
                var shopperId = user.Identity?.Name;
                if (string.IsNullOrEmpty(shopperId))
                    return Results.Unauthorized();

                if (request.Items is null || request.Items.Count == 0)
                    return Results.BadRequest(new { error = "At least one item is required." });

                if (request.Items.Any(i => i.Quantity <= 0 || i.CatalogItemId <= 0))
                    return Results.BadRequest(new { error = "Every item needs a valid catalogItemId and a quantity of at least 1." });

                var ids = request.Items.Select(i => i.CatalogItemId).Distinct().ToArray();
                var catalogItems = await catalogRepository.ListAsync(new CatalogItemsSpecification(ids), cancellationToken);
                var byId = catalogItems.ToDictionary(c => c.Id);

                var missing = ids.Where(id => !byId.ContainsKey(id)).ToArray();
                if (missing.Length > 0)
                    return Results.BadRequest(new { error = $"Unknown catalog item(s): {string.Join(", ", missing)}." });

                var orderItems = request.Items.Select(line =>
                {
                    var catalogItem = byId[line.CatalogItemId];
                    var pictureUri = string.IsNullOrEmpty(catalogItem.PictureUri) ? "eCatalog-item-default.png" : catalogItem.PictureUri;
                    var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, pictureUri);
                    return new OrderItem(itemOrdered, catalogItem.Price, line.Quantity);
                }).ToList();

                // No shipping address is collected by this API; the existing model requires one.
                var shipToAddress = new Address("n/a", "n/a", "n/a", "n/a", "n/a");
                var order = new Order(shopperId, shipToAddress, orderItems);
                await orderRepository.AddAsync(order, cancellationToken);

                // Set aside the round-up. This must never make the order fail.
                long roundUpCents = 0;
                try
                {
                    roundUpCents = await investing.RecordPaidOrderAsync(shopperId, order.Total(), cancellationToken);
                }
                catch (System.Exception ex)
                {
                    logger.LogError(ex, "Setting aside an order's round-up failed; the order still stands.");
                }

                return Results.Ok(new PlaceOrderResponse
                {
                    OrderId = order.Id,
                    RoundUpAmount = roundUpCents / 100m,
                });
            })
            .Produces<PlaceOrderResponse>()
            .WithTags("OrderEndpoints");
    }
}
