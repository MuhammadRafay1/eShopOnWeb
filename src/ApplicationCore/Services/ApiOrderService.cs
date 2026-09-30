using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Configuration;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class ApiOrderService : IApiOrderService
{
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IUriComposer _uriComposer;
    private readonly PayPalSettings _payPalSettings;

    public ApiOrderService(
        IRepository<CatalogItem> itemRepository,
        IRepository<Order> orderRepository,
        IRepository<Payment> paymentRepository,
        IUriComposer uriComposer,
        IOptions<PayPalSettings> payPalSettings)
    {
        _itemRepository = itemRepository;
        _orderRepository = orderRepository;
        _paymentRepository = paymentRepository;
        _uriComposer = uriComposer;
        _payPalSettings = payPalSettings.Value;
    }

    public async Task<Order> PlaceOrderAsync(string buyerId, IReadOnlyList<OrderLineInput> lines, Address shipToAddress, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.Null(lines, nameof(lines));
        if (lines.Count == 0)
        {
            throw new System.ArgumentException("An order must contain at least one line item.", nameof(lines));
        }

        var catalogItemIds = lines.Select(l => l.CatalogItemId).Distinct().ToArray();
        var catalogItemsSpec = new CatalogItemsSpecification(catalogItemIds);
        var catalogItems = await _itemRepository.ListAsync(catalogItemsSpec, cancellationToken);

        var orderItems = new List<OrderItem>();
        foreach (var line in lines)
        {
            Guard.Against.NegativeOrZero(line.Quantity, nameof(line.Quantity));

            var catalogItem = catalogItems.FirstOrDefault(c => c.Id == line.CatalogItemId);
            if (catalogItem is null)
            {
                throw new CatalogItemNotFoundException(line.CatalogItemId);
            }

            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            orderItems.Add(new OrderItem(itemOrdered, catalogItem.Price, line.Quantity));
        }

        var order = new Order(buyerId, shipToAddress, orderItems);
        await _orderRepository.AddAsync(order, cancellationToken);

        var payment = new Payment(order.Id, buyerId, _payPalSettings.Currency, order.Total());
        await _paymentRepository.AddAsync(payment, cancellationToken);

        return order;
    }
}
