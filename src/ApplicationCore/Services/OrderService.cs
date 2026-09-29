using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class OrderService : IOrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IRepository<Basket> _basketRepository;
    private readonly IRepository<CatalogItem> _itemRepository;

    public OrderService(IRepository<Basket> basketRepository,
        IRepository<CatalogItem> itemRepository,
        IRepository<Order> orderRepository,
        IUriComposer uriComposer)
    {
        _orderRepository = orderRepository;
        _uriComposer = uriComposer;
        _basketRepository = basketRepository;
        _itemRepository = itemRepository;
    }

    public async Task CreateOrderAsync(int basketId, Address shippingAddress)
    {
        var basketSpec = new BasketWithItemsSpecification(basketId);
        var basket = await _basketRepository.FirstOrDefaultAsync(basketSpec);

        Guard.Against.Null(basket, nameof(basket));
        Guard.Against.EmptyBasketOnCheckout(basket.Items);

        var catalogItemsSpecification = new CatalogItemsSpecification(basket.Items.Select(item => item.CatalogItemId).ToArray());
        var catalogItems = await _itemRepository.ListAsync(catalogItemsSpecification);

        var items = basket.Items.Select(basketItem =>
        {
            var catalogItem = catalogItems.First(c => c.Id == basketItem.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            var orderItem = new OrderItem(itemOrdered, basketItem.UnitPrice, basketItem.Quantity);
            return orderItem;
        }).ToList();

        var order = new Order(basket.BuyerId, shippingAddress, items);

        await _orderRepository.AddAsync(order);
    }

    public async Task<Order> CreateOrderAsync(string buyerId,
        IReadOnlyCollection<OrderItemRequest> items, Address shippingAddress)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.Null(shippingAddress, nameof(shippingAddress));
        Guard.Against.Null(items, nameof(items));
        if (!items.Any())
        {
            throw new System.ArgumentException("An order must contain at least one item.", nameof(items));
        }
        foreach (var item in items)
        {
            Guard.Against.NegativeOrZero(item.Quantity, nameof(item.Quantity));
        }

        var requestedIds = items.Select(i => i.CatalogItemId).Distinct().ToArray();
        var catalogItemsSpecification = new CatalogItemsSpecification(requestedIds);
        var catalogItems = await _itemRepository.ListAsync(catalogItemsSpecification);

        // Reject unknown catalog ids up front rather than silently dropping them.
        var missing = requestedIds.Except(catalogItems.Select(c => c.Id)).ToArray();
        if (missing.Any())
        {
            throw new System.ArgumentException(
                $"Unknown catalog item id(s): {string.Join(", ", missing)}", nameof(items));
        }

        // Snapshot each requested item from the live catalog at order time (same pattern the
        // basket path uses), so later catalog price/name changes don't mutate this order.
        var orderItems = items.Select(requested =>
        {
            var catalogItem = catalogItems.First(c => c.Id == requested.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name,
                _uriComposer.ComposePicUri(catalogItem.PictureUri));
            return new OrderItem(itemOrdered, catalogItem.Price, requested.Quantity);
        }).ToList();

        var order = new Order(buyerId, shippingAddress, orderItems);

        return await _orderRepository.AddAsync(order);
    }
}
