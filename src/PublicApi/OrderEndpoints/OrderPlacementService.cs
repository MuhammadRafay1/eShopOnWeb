using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public sealed class OrderPlacementService : IOrderPlacementService
{
    // eShopOnWeb has no address capture in this API; reuse the sample storefront's default.
    private static readonly Address DefaultShipToAddress =
        new("123 Main St.", "Kent", "OH", "United States", "44240");

    private readonly IRepository<Order> _orders;
    private readonly IRepository<CatalogItem> _catalogItems;
    private readonly IUriComposer _uriComposer;
    private readonly IInvestingService _investing;
    private readonly ILogger<OrderPlacementService> _logger;

    public OrderPlacementService(
        IRepository<Order> orders,
        IRepository<CatalogItem> catalogItems,
        IUriComposer uriComposer,
        IInvestingService investing,
        ILogger<OrderPlacementService> logger)
    {
        _orders = orders;
        _catalogItems = catalogItems;
        _uriComposer = uriComposer;
        _investing = investing;
        _logger = logger;
    }

    public async Task<OrderPlacementResult> PlaceAsync(string buyerId, IReadOnlyList<OrderLine> lines, CancellationToken cancellationToken = default)
    {
        if (lines is null || lines.Count == 0)
        {
            return new OrderPlacementResult(false, 0, 0m, "At least one order item is required.");
        }

        if (lines.Any(l => l.Quantity <= 0))
        {
            return new OrderPlacementResult(false, 0, 0m, "Each item quantity must be greater than zero.");
        }

        var ids = lines.Select(l => l.CatalogItemId).Distinct().ToArray();
        var catalogItems = await _catalogItems.ListAsync(new CatalogItemsSpecification(ids), cancellationToken).ConfigureAwait(false);
        var catalogById = catalogItems.ToDictionary(c => c.Id);

        var missing = ids.Where(id => !catalogById.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
        {
            return new OrderPlacementResult(false, 0, 0m, $"Unknown catalog item id(s): {string.Join(", ", missing)}.");
        }

        var orderItems = lines.Select(line =>
        {
            var catalogItem = catalogById[line.CatalogItemId];
            var itemOrdered = new CatalogItemOrdered(
                catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            return new OrderItem(itemOrdered, catalogItem.Price, line.Quantity);
        }).ToList();

        var order = new Order(buyerId, DefaultShipToAddress, orderItems);
        await _orders.AddAsync(order, cancellationToken).ConfigureAwait(false);

        // In eShopOnWeb there is no separate payment step: placing the order pays for it.
        var total = order.Total();

        // Setting aside the change must never fail the order.
        var roundUp = 0m;
        try
        {
            var setAside = await _investing.ApplyPaidOrderAsync(buyerId, total, cancellationToken).ConfigureAwait(false);
            roundUp = setAside.RoundUpAmount;
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Setting aside change for order {OrderId} failed; the order was still placed.", order.Id);
        }

        return new OrderPlacementResult(true, order.Id, roundUp, null);
    }
}
