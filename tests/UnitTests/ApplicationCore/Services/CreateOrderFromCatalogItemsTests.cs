using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services;

public class CreateOrderFromCatalogItemsTests
{
    private readonly IRepository<Basket> _basketRepo = Substitute.For<IRepository<Basket>>();
    private readonly IRepository<CatalogItem> _itemRepo = Substitute.For<IRepository<CatalogItem>>();
    private readonly IRepository<Order> _orderRepo = Substitute.For<IRepository<Order>>();
    private readonly IUriComposer _uriComposer = Substitute.For<IUriComposer>();

    private OrderService CreateService()
    {
        _uriComposer.ComposePicUri(Arg.Any<string>()).Returns("pic.png");
        _orderRepo.AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>()).Returns(ci => ci.Arg<Order>());
        return new OrderService(_basketRepo, _itemRepo, _orderRepo, _uriComposer);
    }

    private static CatalogItem Item(int id, decimal price)
    {
        var ci = new CatalogItem(1, 1, "desc", $"Item {id}", price, "pic.png");
        // BaseEntity.Id has a protected setter; set it via reflection for the test.
        typeof(CatalogItem).GetProperty("Id")!.SetValue(ci, id);
        return ci;
    }

    [Fact]
    public async Task BuildsOrderFromCatalogPrices()
    {
        _itemRepo.ListAsync(Arg.Any<CatalogItemsSpecification>(), Arg.Any<CancellationToken>())
            .Returns(new List<CatalogItem> { Item(1, 10m), Item(2, 5m) });

        var svc = CreateService();
        var order = await svc.CreateOrderFromCatalogItemsAsync(
            "buyer@x.com",
            new Address("s", "c", "st", "US", "12345"),
            new List<(int, int)> { (1, 2), (2, 3) });

        Assert.Equal(2 * 10m + 3 * 5m, order.Total());
        Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
        await _orderRepo.Received(1).AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnknownCatalogItem_Throws()
    {
        _itemRepo.ListAsync(Arg.Any<CatalogItemsSpecification>(), Arg.Any<CancellationToken>())
            .Returns(new List<CatalogItem> { Item(1, 10m) });

        var svc = CreateService();
        await Assert.ThrowsAsync<CatalogItemNotFoundException>(() =>
            svc.CreateOrderFromCatalogItemsAsync("buyer@x.com", new Address("s", "c", "st", "US", "12345"),
                new List<(int, int)> { (99, 1) }));
    }

    [Fact]
    public async Task EmptyItems_Throws()
    {
        var svc = CreateService();
        await Assert.ThrowsAsync<InvalidPaymentRequestException>(() =>
            svc.CreateOrderFromCatalogItemsAsync("buyer@x.com", new Address("s", "c", "st", "US", "12345"),
                new List<(int, int)>()));
    }

    [Fact]
    public async Task NonPositiveQuantity_Throws()
    {
        var svc = CreateService();
        await Assert.ThrowsAsync<InvalidPaymentRequestException>(() =>
            svc.CreateOrderFromCatalogItemsAsync("buyer@x.com", new Address("s", "c", "st", "US", "12345"),
                new List<(int, int)> { (1, 0) }));
    }
}
