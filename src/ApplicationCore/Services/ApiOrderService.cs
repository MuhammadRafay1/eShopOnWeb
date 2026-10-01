using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Payments;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Places an order straight from catalog item ids + quantities (no basket), reusing the existing Order/OrderItem
/// model. Prices are always read from the catalog — never trusted from the caller.
/// </summary>
public sealed class ApiOrderService : IApiOrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IReadRepository<CatalogItem> _catalogRepository;

    public ApiOrderService(IRepository<Order> orderRepository, IReadRepository<CatalogItem> catalogRepository)
    {
        _orderRepository = orderRepository;
        _catalogRepository = catalogRepository;
    }

    public async Task<Order> PlaceOrderAsync(string buyerId, IReadOnlyList<OrderLine> lines, Address shipToAddress, CancellationToken cancellationToken)
    {
        if (lines is null || lines.Count == 0)
        {
            throw new BadPaymentRequestException("An order must contain at least one item.");
        }

        var items = new List<OrderItem>();
        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
            {
                throw new BadPaymentRequestException($"Quantity for catalog item {line.CatalogItemId} must be greater than zero.");
            }

            var catalogItem = await _catalogRepository.GetByIdAsync(line.CatalogItemId, cancellationToken);
            if (catalogItem is null)
            {
                throw new BadPaymentRequestException($"Catalog item {line.CatalogItemId} was not found.");
            }

            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, catalogItem.PictureUri);
            items.Add(new OrderItem(itemOrdered, catalogItem.Price, line.Quantity));
        }

        var order = new Order(buyerId, shipToAddress, items);
        await _orderRepository.AddAsync(order, cancellationToken);
        return order;
    }
}
