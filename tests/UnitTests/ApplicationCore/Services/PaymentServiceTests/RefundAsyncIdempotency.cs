using Microsoft.eShopWeb.ApplicationCore;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.UnitTests.Builders;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.PaymentServiceTests;

public class RefundAsyncIdempotency
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

    private (Order Order, Payment Payment) FulfilledOrderAndPayment(decimal capturedAmount = 100m)
    {
        var order = new OrderBuilder().WithDefaultValues();
        order.MarkAuthorized();
        order.MarkFulfilled();

        var payment = new Payment(1, order.BuyerId, "USD", capturedAmount);
        payment.RecordAuthorization("ppOrder", "auth1", "CREATED", null, "VISA", "1111");
        payment.RecordCapture("capture1", "COMPLETED", capturedAmount, 5m, capturedAmount - 5m);

        _orderRepo.FirstOrDefaultAsync(Arg.Any<OrderWithItemsByIdSpec>(), default).Returns(order);
        _paymentRepo.FirstOrDefaultAsync(Arg.Any<PaymentByOrderIdSpec>(), default).Returns(payment);

        return (order, payment);
    }

    [Fact]
    public async Task RepeatingSameIdempotencyKey_ReturnsOriginalRefund_WithoutCallingPayPalAgain()
    {
        var (order, payment) = FulfilledOrderAndPayment();

        _payPalClient.RefundAsync("capture1", 30m, "USD", "refund-key-1")
            .Returns(new RefundResult("refund1", "COMPLETED", 30m, 30m));

        var service = BuildService();

        var first = await service.RefundAsync(1, order.BuyerId, 30m, "refund-key-1", null);
        var second = await service.RefundAsync(1, order.BuyerId, 30m, "refund-key-1", null);

        Assert.Equal(first.Refund.PayPalRefundId, second.Refund.PayPalRefundId);
        await _payPalClient.Received(1).RefundAsync(Arg.Any<string>(), Arg.Any<decimal?>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task TwoDistinctIdempotencyKeys_BothIssueLegitimatePartialRefunds()
    {
        var (order, payment) = FulfilledOrderAndPayment();

        _payPalClient.RefundAsync("capture1", 40m, "USD", "key-a").Returns(new RefundResult("refundA", "COMPLETED", 40m, 40m));
        _payPalClient.RefundAsync("capture1", 60m, "USD", "key-b").Returns(new RefundResult("refundB", "COMPLETED", 60m, 100m));

        var service = BuildService();

        await service.RefundAsync(1, order.BuyerId, 40m, "key-a", null);
        await service.RefundAsync(1, order.BuyerId, 60m, "key-b", null);

        Assert.Equal(100m, payment.TotalRefunded());
        await _payPalClient.Received(2).RefundAsync(Arg.Any<string>(), Arg.Any<decimal?>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task RefundExceedingCapturedAmount_ThrowsWithoutCallingPayPal()
    {
        var (order, _) = FulfilledOrderAndPayment(capturedAmount: 50m);
        var service = BuildService();

        await Assert.ThrowsAsync<RefundExceedsCaptureException>(() =>
            service.RefundAsync(1, order.BuyerId, 50.01m, "key-too-big", null));

        await _payPalClient.DidNotReceive().RefundAsync(Arg.Any<string>(), Arg.Any<decimal?>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task RefundForAnotherBuyersOrder_ThrowsNotFound()
    {
        var (order, _) = FulfilledOrderAndPayment();
        var service = BuildService();

        await Assert.ThrowsAsync<OrderNotFoundException>(() =>
            service.RefundAsync(1, "someone-else@test.com", 10m, "key-x", null));
    }
}
