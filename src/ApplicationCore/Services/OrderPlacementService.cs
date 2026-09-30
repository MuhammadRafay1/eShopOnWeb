using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ardalis.Result;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class OrderPlacementService : IOrderPlacementService
{
    // Mirrors the storefront's default shipping address (src/Web/Pages/Basket/Checkout.cshtml.cs),
    // used when a caller doesn't supply one.
    private static readonly Address DefaultShipToAddress = new("123 Main St.", "Kent", "OH", "United States", "44240");

    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<CatalogItem> _catalogItemRepository;

    public OrderPlacementService(IRepository<Order> orderRepository, IRepository<CatalogItem> catalogItemRepository)
    {
        _orderRepository = orderRepository;
        _catalogItemRepository = catalogItemRepository;
    }

    public async Task<Result<Order>> PlaceOrderAsync(string buyerId, IReadOnlyList<OrderItemRequestLine> items, Address? shipToAddress)
    {
        if (items is null || items.Count == 0)
        {
            return Result<Order>.Invalid(new List<ValidationError> { new() { Identifier = "items", ErrorMessage = "An order must contain at least one item." } });
        }

        var orderItems = new List<OrderItem>();
        foreach (var line in items)
        {
            if (line.Quantity <= 0)
            {
                return Result<Order>.Invalid(new List<ValidationError> { new() { Identifier = "items", ErrorMessage = $"Quantity for catalog item {line.CatalogItemId} must be positive." } });
            }

            var catalogItem = await _catalogItemRepository.GetByIdAsync(line.CatalogItemId);
            if (catalogItem is null)
            {
                return Result<Order>.Invalid(new List<ValidationError> { new() { Identifier = "items", ErrorMessage = $"Catalog item {line.CatalogItemId} does not exist." } });
            }

            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, catalogItem.PictureUri);
            orderItems.Add(new OrderItem(itemOrdered, catalogItem.Price, line.Quantity));
        }

        var order = new Order(buyerId, shipToAddress ?? DefaultShipToAddress, orderItems);
        await _orderRepository.AddAsync(order);

        return Result<Order>.Success(order);
    }
}
