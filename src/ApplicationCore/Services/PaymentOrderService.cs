using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class PaymentOrderService : IPaymentOrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly IUriComposer _uriComposer;
    private readonly string _currency;
    private readonly string _instanceId;

    private static readonly Address PlaceholderShipToAddress = new("123 Main St", "Redmond", "WA", "USA", "98052");

    public PaymentOrderService(
        IRepository<Order> orderRepository,
        IRepository<Payment> paymentRepository,
        IRepository<CatalogItem> itemRepository,
        IUriComposer uriComposer,
        string currency,
        string instanceId)
    {
        _orderRepository = orderRepository;
        _paymentRepository = paymentRepository;
        _itemRepository = itemRepository;
        _uriComposer = uriComposer;
        _currency = currency;
        _instanceId = instanceId;
    }

    public async Task<PlaceOrderResult> PlaceOrderAsync(string buyerId, IEnumerable<(int CatalogItemId, int Quantity)> items, Address? shipToAddress, CancellationToken ct = default)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));

        var requestedItems = items.ToList();
        if (requestedItems.Count == 0)
        {
            throw new ArgumentException("An order must contain at least one item.", nameof(items));
        }
        if (requestedItems.Any(i => i.Quantity < 1))
        {
            throw new ArgumentException("Item quantities must be at least 1.", nameof(items));
        }

        var ids = requestedItems.Select(i => i.CatalogItemId).Distinct().ToArray();
        var catalogItems = await _itemRepository.ListAsync(new CatalogItemsSpecification(ids), ct);
        var missing = ids.Except(catalogItems.Select(c => c.Id)).ToList();
        if (missing.Count > 0)
        {
            throw new ArgumentException($"Unknown catalog item id(s): {string.Join(", ", missing)}.", nameof(items));
        }

        var orderItems = requestedItems.Select(requested =>
        {
            var catalogItem = catalogItems.First(c => c.Id == requested.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            return new OrderItem(itemOrdered, catalogItem.Price, requested.Quantity);
        }).ToList();

        var order = new Order(buyerId, shipToAddress ?? PlaceholderShipToAddress, orderItems);
        order = await _orderRepository.AddAsync(order, ct);

        var invoiceReference = $"ESHOP-{_instanceId}-{order.Id}";
        var payment = new Payment(order.Id, buyerId, order.Total(), _currency, invoiceReference);
        await _paymentRepository.AddAsync(payment, ct);

        return new PlaceOrderResult(order.Id, order.Total(), _currency, payment.Status.ToString());
    }
}
