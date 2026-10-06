using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Places an order for a shopper directly from catalog item ids and quantities, reusing the app's existing
/// <see cref="Order"/>/<see cref="OrderItem"/> model (not a parallel one). The caller's identity is the
/// authenticated shop user.
/// </summary>
public sealed class OrderPlacementService : IOrderPlacementService
{
    // The POST /api/orders surface carries only catalog items and quantities; a placeholder ship-to
    // address satisfies the order model without inventing a new request shape.
    private static readonly Address PlaceholderAddress = new("n/a", "n/a", "n/a", "n/a", "n/a");

    private readonly IRepository<Order> _orders;
    private readonly IRepository<CatalogItem> _catalogItems;
    private readonly IUriComposer _uriComposer;

    public OrderPlacementService(
        IRepository<Order> orders, IRepository<CatalogItem> catalogItems, IUriComposer uriComposer)
    {
        _orders = orders;
        _catalogItems = catalogItems;
        _uriComposer = uriComposer;
    }

    public async Task<PlacedOrderResult> PlaceOrderAsync(
        string buyerId, IReadOnlyList<OrderLine> lines, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        if (lines is null || lines.Count == 0)
            throw new ArgumentException("An order must contain at least one item.", nameof(lines));
        if (lines.Any(l => l.Quantity <= 0))
            throw new ArgumentException("Each order line must have a positive quantity.", nameof(lines));

        var ids = lines.Select(l => l.CatalogItemId).Distinct().ToArray();
        var catalogItems = await _catalogItems.ListAsync(new CatalogItemsSpecification(ids), cancellationToken);

        var items = new List<OrderItem>();
        foreach (var line in lines)
        {
            var catalogItem = catalogItems.FirstOrDefault(c => c.Id == line.CatalogItemId)
                ?? throw new ArgumentException($"Catalog item {line.CatalogItemId} does not exist.", nameof(lines));

            var ordered = new CatalogItemOrdered(
                catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            items.Add(new OrderItem(ordered, catalogItem.Price, line.Quantity));
        }

        var order = new Order(buyerId, PlaceholderAddress, items);
        await _orders.AddAsync(order, cancellationToken);

        return new PlacedOrderResult(order.Id, order.Total());
    }
}
