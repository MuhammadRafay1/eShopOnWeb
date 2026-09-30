using Microsoft.eShopWeb.ApplicationCore;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.UnitTests.Builders;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.PaymentServiceTests;

public class PayAsyncIdempotency
{
    private readonly IRepository<Order> _orderRepo = Substitute.For<IRepository<Order>>();
    private readonly IRepository<Payment> _paymentRepo = Substitute.For<IRepository<Payment>>();
    private readonly IRepository<CatalogItem> _catalogItemRepo = Substitute.For<IRepository<CatalogItem>>();
    private readonly IRepository<SavedPaymentMethod> _savedPaymentMethodRepo = Substitute.For<IRepository<SavedPaymentMethod>>();
    private readonly IUriComposer _uriComposer = Substitute.For<IUriComposer>();
    private readonly IPayPalClient _payPalClient = Substitute.For<IPayPalClient>();

    private PaymentService BuildService() => new(
        _orderRepo, _paymentRepo, _catalogItemRepo, _savedPaymentMethodRepo, _uriComposer, _payPalClient,
        new PayPalSettings { ClientId = "id", ClientSecret = "secret", Environment = "sandbox", Currency = "USD" });

    private static CardDetails TestCard => new(
        "Jane Shopper", "4111111111111111", "2030-01", "123",
        new PayPalBillingAddress("1 Main St", null, "Redmond", "WA", "98052", "US"));

    [Fact]
    public async Task SecondPayCallForAlreadyAuthorizedOrder_ReturnsExistingPayment_WithoutCallingPayPalAgain()
    {
        var order = new OrderBuilder().WithDefaultValues();
        order.MarkAuthorized();
        _orderRepo.FirstOrDefaultAsync(Arg.Any<OrderWithItemsByIdSpec>(), default).Returns(order);

        var existingPayment = new Payment(1, order.BuyerId, "USD", order.Total());
        existingPayment.RecordAuthorization("ppOrder", "auth1", "CREATED", null, "VISA", "1111");
        _paymentRepo.FirstOrDefaultAsync(Arg.Any<PaymentByOrderIdSpec>(), default).Returns(existingPayment);

        var service = BuildService();

        var result = await service.PayWithCardAsync(1, order.BuyerId, TestCard);

        Assert.Same(existingPayment, result);
        await _payPalClient.DidNotReceive().AuthorizeOrderWithCardAsync(
            Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CardDetails>(), Arg.Any<string>());
    }

    [Fact]
    public async Task FirstPayCall_AuthorizesForTheOrderTotal_ToTheCent()
    {
        var order = new OrderBuilder().WithDefaultValues();
        _orderRepo.FirstOrDefaultAsync(Arg.Any<OrderWithItemsByIdSpec>(), default).Returns(order);
        _paymentRepo.FirstOrDefaultAsync(Arg.Any<PaymentByOrderIdSpec>(), default).Returns((Payment?)null);

        _payPalClient.AuthorizeOrderWithCardAsync(Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CardDetails>(), Arg.Any<string>())
            .Returns(new AuthorizationResult("ppOrder1", "auth1", "CREATED", null, "VISA", "1111", false, null));

        var service = BuildService();

        var result = await service.PayWithCardAsync(1, order.BuyerId, TestCard);

        Assert.Equal("auth1", result.AuthorizationId);
        await _payPalClient.Received(1).AuthorizeOrderWithCardAsync(order.Total(), "USD", Arg.Any<string>(), Arg.Any<string>(), TestCard, "pay-1");
    }
}
