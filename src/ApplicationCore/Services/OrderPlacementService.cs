using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class OrderPlacementService : IOrderPlacementService
{
    // No shipping step exists in this flow, so a placeholder ship-to address is used (as the storefront does).
    private static readonly Address DefaultShippingAddress = new("123 Main St.", "Kent", "OH", "United States", "44240");

    private readonly IBasketService _basketService;
    private readonly IOrderService _orderService;
    private readonly IRepository<CatalogItem> _catalogItemRepository;

    public OrderPlacementService(IBasketService basketService, IOrderService orderService, IRepository<CatalogItem> catalogItemRepository)
    {
        _basketService = basketService;
        _orderService = orderService;
        _catalogItemRepository = catalogItemRepository;
    }

    public async Task<Order> PlaceOrderAsync(string buyerId, IReadOnlyCollection<OrderLine> lines, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        if (lines is null || lines.Count == 0)
        {
            throw new OrderPlacementException("An order must contain at least one item.");
        }

        var basketId = 0;
        foreach (var line in lines)
        {
            if (line.Quantity < 1)
            {
                throw new OrderPlacementException($"Quantity for catalog item {line.CatalogItemId} must be at least 1.");
            }

            var catalogItem = await _catalogItemRepository.GetByIdAsync(line.CatalogItemId, cancellationToken);
            if (catalogItem is null)
            {
                throw new OrderPlacementException($"Catalog item {line.CatalogItemId} does not exist.");
            }

            var basket = await _basketService.AddItemToBasket(buyerId, catalogItem.Id, catalogItem.Price, line.Quantity);
            basketId = basket.Id;
        }

        var order = await _orderService.CreateOrderAsync(basketId, DefaultShippingAddress);
        await _basketService.DeleteBasketAsync(basketId);
        return order;
    }
}
