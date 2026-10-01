using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class OrderPlacementService : IOrderPlacementService
{
    private static readonly Address DefaultShipToAddress = new("123 Main St.", "Kent", "OH", "United States", "44240");

    private readonly IRepository<Order> _orderRepository;
    private readonly IReadRepository<CatalogItem> _catalogItemRepository;
    private readonly IUriComposer _uriComposer;

    public OrderPlacementService(IRepository<Order> orderRepository, IReadRepository<CatalogItem> catalogItemRepository, IUriComposer uriComposer)
    {
        _orderRepository = orderRepository;
        _catalogItemRepository = catalogItemRepository;
        _uriComposer = uriComposer;
    }

    public async Task<Order> PlaceOrderAsync(string buyerId, IReadOnlyList<OrderItemInput> items, Address? shipToAddress, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(items, nameof(items));

        var catalogItemIds = items.Select(i => i.CatalogItemId).ToArray();
        var catalogItemsSpec = new CatalogItemsSpecification(catalogItemIds);
        var catalogItems = await _catalogItemRepository.ListAsync(catalogItemsSpec, cancellationToken);

        var orderItems = items.Select(item =>
        {
            var catalogItem = catalogItems.FirstOrDefault(c => c.Id == item.CatalogItemId)
                ?? throw new ArgumentOutOfRangeException(nameof(items), item.CatalogItemId, "Catalog item not found.");
            Guard.Against.NegativeOrZero(item.Quantity, nameof(item.Quantity));

            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            return new OrderItem(itemOrdered, catalogItem.Price, item.Quantity);
        }).ToList();

        var order = new Order(buyerId, shipToAddress ?? DefaultShipToAddress, orderItems);
        await _orderRepository.AddAsync(order, cancellationToken);
        return order;
    }
}
