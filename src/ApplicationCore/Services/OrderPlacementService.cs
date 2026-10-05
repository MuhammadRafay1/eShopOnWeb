using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Places an order from catalog item ids and quantities for the signed-in shopper, reusing the existing
/// <see cref="Order"/> aggregate. eShopOnWeb has no separate payment step, so an order placed here is
/// treated as paid, and the change is set aside for an enrolled shopper. Investing never fails the order.
/// </summary>
public class OrderPlacementService : IOrderPlacementService
{
    // The storefront order model requires a ship-to address; API-placed change-investing orders have none,
    // so a placeholder is used (the address is not part of this feature).
    private static readonly Address PlaceholderAddress = new("N/A", "N/A", "N/A", "N/A", "00000");

    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IInvestingService _investingService;

    public OrderPlacementService(
        IRepository<Order> orderRepository,
        IRepository<CatalogItem> itemRepository,
        IUriComposer uriComposer,
        IInvestingService investingService)
    {
        _orderRepository = orderRepository;
        _itemRepository = itemRepository;
        _uriComposer = uriComposer;
        _investingService = investingService;
    }

    public async Task<OrderPlacedResult> PlaceOrderAsync(string buyerId, IReadOnlyList<OrderLine> lines, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        if (lines is null || lines.Count == 0)
            throw new OrderItemsRequiredException();

        var ids = lines.Select(l => l.CatalogItemId).Distinct().ToArray();
        var catalogItems = await _itemRepository.ListAsync(new CatalogItemsSpecification(ids), cancellationToken);
        var byId = catalogItems.ToDictionary(c => c.Id);

        var orderItems = new List<OrderItem>();
        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
                throw new InvalidOrderLineException($"Quantity for catalog item {line.CatalogItemId} must be positive.");
            if (!byId.TryGetValue(line.CatalogItemId, out var catalogItem))
                throw new InvalidOrderLineException($"Catalog item {line.CatalogItemId} does not exist.");

            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            orderItems.Add(new OrderItem(itemOrdered, catalogItem.Price, line.Quantity));
        }

        var order = new Order(buyerId, PlaceholderAddress, orderItems);
        await _orderRepository.AddAsync(order, cancellationToken);

        // Order is placed and paid; setting aside the change must never fail the order.
        var roundUp = await _investingService.RecordPaidOrderAsync(buyerId, order.Total(), cancellationToken);

        return new OrderPlacedResult(order.Id, roundUp);
    }
}
