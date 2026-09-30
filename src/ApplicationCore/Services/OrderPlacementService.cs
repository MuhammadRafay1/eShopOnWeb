using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Models.Orders;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Builds an Order directly from catalog item ids/quantities (the PublicApi surface has no basket),
/// reusing the existing Order/OrderItem aggregate as-is, and opens the AwaitingPayment Payment for it.
/// </summary>
public class OrderPlacementService : IOrderPlacementService
{
    // Mirrors the hard-coded default shipping address the Web checkout flow uses
    // (src/Web/Pages/Basket/Checkout.cshtml.cs) — the task does not require address capture.
    private static readonly Address DefaultShipToAddress = new("123 Main St.", "Kent", "OH", "United States", "44240");

    private readonly IRepository<CatalogItem> _catalogItemRepository;
    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IPaymentGatewaySettings _gatewaySettings;

    public OrderPlacementService(
        IRepository<CatalogItem> catalogItemRepository,
        IRepository<Order> orderRepository,
        IRepository<Payment> paymentRepository,
        IUriComposer uriComposer,
        IPaymentGatewaySettings gatewaySettings)
    {
        _catalogItemRepository = catalogItemRepository;
        _orderRepository = orderRepository;
        _paymentRepository = paymentRepository;
        _uriComposer = uriComposer;
        _gatewaySettings = gatewaySettings;
    }

    public async Task<OrderPlacementResult> PlaceOrderAsync(string buyerId, IReadOnlyList<OrderPlacementItem> items, Address? shipToAddress, CancellationToken ct = default)
    {
        if (items is null || items.Count == 0)
        {
            throw new RequestValidationException("At least one item is required to place an order.");
        }

        if (items.Any(i => i.Quantity < 1))
        {
            throw new RequestValidationException("Item quantities must be at least 1.");
        }

        var ids = items.Select(i => i.CatalogItemId).Distinct().ToArray();
        var catalogItems = await _catalogItemRepository.ListAsync(new CatalogItemsSpecification(ids), ct);

        var missingIds = ids.Except(catalogItems.Select(c => c.Id)).ToList();
        if (missingIds.Count > 0)
        {
            throw new RequestValidationException($"Unknown catalog item id(s): {string.Join(", ", missingIds)}.");
        }

        var orderItems = items.Select(item =>
        {
            var catalogItem = catalogItems.First(c => c.Id == item.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            return new OrderItem(itemOrdered, catalogItem.Price, item.Quantity);
        }).ToList();

        var order = new Order(buyerId, shipToAddress ?? DefaultShipToAddress, orderItems);
        await _orderRepository.AddAsync(order, ct);

        var correlationReference = $"ESHOP-{order.Id}-{Guid.NewGuid():N}"[..24];
        var payment = new Payment(order.Id, buyerId, order.Total(), _gatewaySettings.CurrencyCode, correlationReference);
        await _paymentRepository.AddAsync(payment, ct);

        return new OrderPlacementResult
        {
            OrderId = order.Id,
            OrderDate = order.OrderDate,
            Total = order.Total(),
            CurrencyCode = _gatewaySettings.CurrencyCode
        };
    }
}
