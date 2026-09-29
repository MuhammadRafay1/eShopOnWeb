using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
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

        var items = await BuildOrderItemsAsync(
            basket.Items.Select(i => (i.CatalogItemId, i.UnitPrice, i.Quantity)).ToList());

        var order = new Order(basket.BuyerId, shippingAddress, items);

        await _orderRepository.AddAsync(order);
    }

    public async Task<Order> CreatePendingOrderAsync(string buyerId, Address shippingAddress,
        IReadOnlyCollection<(int CatalogItemId, int Quantity)> items)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.Null(items, nameof(items));
        if (!items.Any())
            throw new OrderItemsRequiredException();

        var catalogItemsSpec = new CatalogItemsSpecification(items.Select(i => i.CatalogItemId).Distinct().ToArray());
        var catalogItems = await _itemRepository.ListAsync(catalogItemsSpec);

        var priced = new List<(int CatalogItemId, decimal UnitPrice, int Quantity)>();
        foreach (var (catalogItemId, quantity) in items)
        {
            if (quantity <= 0)
                throw new InvalidOrderItemException($"Quantity for catalog item {catalogItemId} must be greater than zero.");

            var catalogItem = catalogItems.FirstOrDefault(c => c.Id == catalogItemId);
            if (catalogItem is null)
                throw new InvalidOrderItemException($"Catalog item {catalogItemId} does not exist.");

            priced.Add((catalogItemId, catalogItem.Price, quantity));
        }

        var orderItems = await BuildOrderItemsAsync(priced);
        var order = new Order(buyerId, shippingAddress, orderItems);

        return await _orderRepository.AddAsync(order);
    }

    /// <summary>
    /// Shared catalog-lookup + OrderItem construction used by both the basket checkout flow and the
    /// direct API order-placement flow, so both build the exact same Order/OrderItem model.
    /// </summary>
    private async Task<List<OrderItem>> BuildOrderItemsAsync(
        IReadOnlyCollection<(int CatalogItemId, decimal UnitPrice, int Quantity)> lines)
    {
        var catalogItemsSpecification = new CatalogItemsSpecification(lines.Select(l => l.CatalogItemId).Distinct().ToArray());
        var catalogItems = await _itemRepository.ListAsync(catalogItemsSpecification);

        return lines.Select(line =>
        {
            var catalogItem = catalogItems.First(c => c.Id == line.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            return new OrderItem(itemOrdered, line.UnitPrice, line.Quantity);
        }).ToList();
    }
}
