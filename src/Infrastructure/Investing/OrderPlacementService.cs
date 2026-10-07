using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Places an order straight onto the existing <see cref="Order"/> aggregate from a
/// list of catalog item ids + quantities, then — because this app has no separate
/// payment step — treats the order as paid and asks the investing service to set
/// aside the round-up. Reuses the shop's order/order-item model rather than a
/// parallel one.
/// </summary>
public sealed class OrderPlacementService : IOrderPlacementService
{
    // This API carries no shipping step; orders are catalog purchases only.
    private static readonly Address NoShippingAddress = new("N/A", "N/A", "N/A", "N/A", "N/A");

    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly IRepository<Order> _orderRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IInvestingService _investingService;

    public OrderPlacementService(
        IRepository<CatalogItem> itemRepository,
        IRepository<Order> orderRepository,
        IUriComposer uriComposer,
        IInvestingService investingService)
    {
        _itemRepository = itemRepository;
        _orderRepository = orderRepository;
        _uriComposer = uriComposer;
        _investingService = investingService;
    }

    public async Task<OrderPlacedResult> PlaceOrderAsync(
        string buyerId, IReadOnlyCollection<OrderLineRequest> lines, CancellationToken ct)
    {
        // Merge duplicate lines and ignore non-positive quantities.
        var quantities = lines
            .Where(l => l.Quantity > 0)
            .GroupBy(l => l.CatalogItemId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        if (quantities.Count == 0)
            throw new ArgumentException("An order must contain at least one item with a positive quantity.");

        var catalogItems = await _itemRepository.ListAsync(new CatalogItemsSpecification(quantities.Keys.ToArray()), ct);
        var missing = quantities.Keys.Where(id => catalogItems.All(c => c.Id != id)).ToList();
        if (missing.Count > 0)
            throw new ArgumentException($"Unknown catalog item id(s): {string.Join(", ", missing)}.");

        var items = quantities.Select(kv =>
        {
            var catalogItem = catalogItems.First(c => c.Id == kv.Key);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            return new OrderItem(itemOrdered, catalogItem.Price, kv.Value);
        }).ToList();

        var order = new Order(buyerId, NoShippingAddress, items);
        await _orderRepository.AddAsync(order, ct);

        // No separate payment step: the order is paid now, so set aside the change.
        // This never throws — it must not fail the order.
        var roundUp = await _investingService.SetAsideForPaidOrderAsync(buyerId, order.Id, order.Total(), ct);

        return new OrderPlacedResult(order.Id, roundUp);
    }
}
