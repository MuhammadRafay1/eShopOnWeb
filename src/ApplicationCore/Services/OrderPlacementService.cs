using System;
using System.Collections.Generic;
using Ardalis.GuardClauses;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Default <see cref="IOrderPlacementService"/>. Builds an <see cref="Order"/> from catalog
/// items — the app's existing order model — treats placing it as the point of payment, and
/// hands the paid total to <see cref="IInvestingService"/> to set aside the rounding
/// difference. Investing never makes order placement fail.
/// </summary>
public class OrderPlacementService : IOrderPlacementService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly IInvestingService _investingService;
    private readonly IUriComposer _uriComposer;

    public OrderPlacementService(
        IRepository<Order> orderRepository,
        IRepository<CatalogItem> itemRepository,
        IInvestingService investingService,
        IUriComposer uriComposer)
    {
        _orderRepository = orderRepository;
        _itemRepository = itemRepository;
        _investingService = investingService;
        _uriComposer = uriComposer;
    }

    public async Task<OrderPlacementResult> PlaceAsync(string buyerId, IReadOnlyCollection<OrderLine> lines, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        if (lines is null || lines.Count == 0)
        {
            throw new ArgumentException("An order must contain at least one item.", nameof(lines));
        }

        var orderItems = new List<OrderItem>();
        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
            {
                throw new ArgumentException($"Quantity for catalog item {line.CatalogItemId} must be positive.", nameof(lines));
            }

            var catalogItem = await _itemRepository.GetByIdAsync(line.CatalogItemId, cancellationToken);
            if (catalogItem is null)
            {
                throw new ArgumentException($"Catalog item {line.CatalogItemId} does not exist.", nameof(lines));
            }

            var itemOrdered = new CatalogItemOrdered(
                catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            orderItems.Add(new OrderItem(itemOrdered, catalogItem.Price, line.Quantity));
        }

        // eShopOnWeb has no separate payment step; placing the order here is where it is paid.
        // The shipping address is not part of the investing capability.
        var shipToAddress = new Address("N/A", "N/A", "N/A", "N/A", "N/A");
        var order = new Order(buyerId, shipToAddress, orderItems);
        await _orderRepository.AddAsync(order, cancellationToken);

        // Set aside this paid order's rounding difference. Never throws.
        var roundUp = await _investingService.ApplyPaidOrderAsync(buyerId, order.Total(), cancellationToken);

        return new OrderPlacementResult(order.Id, roundUp);
    }
}
