using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PaymentGateway;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.OrderPaymentServiceTests;

public class RefundTests : OrderPaymentServiceTestBase
{
    private (Order order, OrderPayment payment) ArrangeCaptured(decimal captured = 31.50m)
    {
        var order = NewOrder();
        order.MarkAuthorized();
        order.MarkFulfilled();
        var payment = PaymentFor(captured);
        payment.RecordCapture("CAP1", captured, 1.31m, captured - 1.31m);
        OrderRepo.GetByIdAsync(OrderId, Arg.Any<CancellationToken>()).Returns(order);
        PaymentRepo.FirstOrDefaultAsync(Arg.Any<OrderPaymentByOrderIdSpecification>(), Arg.Any<CancellationToken>()).Returns(payment);
        return (order, payment);
    }

    [Fact]
    public async Task PartialRefund_ReducesRemaining_AndMarksPartiallyRefunded()
    {
        var (order, payment) = ArrangeCaptured(31.50m);
        Gateway.RefundCaptureAsync(Arg.Any<string>(), "CAP1", 10m, "USD", Arg.Any<CancellationToken>())
            .Returns(new RefundResult("PPR1", "COMPLETED", 10m, 10m));

        var result = await CreateService().RefundAsync(OrderId, BuyerId, 10m, "ref-A", CancellationToken.None);

        Assert.Equal(10m, result.Amount);
        Assert.Equal(21.50m, result.RemainingRefundable);
        Assert.Equal(OrderStatus.PartiallyRefunded, order.Status);
    }

    [Fact]
    public async Task FullRemainingRefund_WhenNoAmount_MarksRefunded()
    {
        var (order, payment) = ArrangeCaptured(31.50m);
        Gateway.RefundCaptureAsync(Arg.Any<string>(), "CAP1", 31.50m, "USD", Arg.Any<CancellationToken>())
            .Returns(new RefundResult("PPR1", "COMPLETED", 31.50m, 31.50m));

        var result = await CreateService().RefundAsync(OrderId, BuyerId, null, "ref-full", CancellationToken.None);

        Assert.Equal(31.50m, result.Amount);
        Assert.Equal(0m, result.RemainingRefundable);
        Assert.Equal(OrderStatus.Refunded, order.Status);
    }

    [Fact]
    public async Task RepeatedKey_ReturnsExistingRefund_WithoutCallingPayPal()
    {
        var (order, payment) = ArrangeCaptured(31.50m);
        payment.RecordRefund(new Refund("PPR-existing", 10m, "ref-A"));

        var result = await CreateService().RefundAsync(OrderId, BuyerId, 10m, "ref-A", CancellationToken.None);

        Assert.Equal("PPR-existing", result.PayPalRefundId);
        await Gateway.DidNotReceive().RefundCaptureAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<decimal?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OverRefund_Throws400_BeforeCallingPayPal()
    {
        var (order, payment) = ArrangeCaptured(31.50m);
        payment.RecordRefund(new Refund("PPR1", 20m, "ref-A")); // remaining 11.50

        await Assert.ThrowsAsync<InvalidPaymentRequestException>(() =>
            CreateService().RefundAsync(OrderId, BuyerId, 20m, "ref-B", CancellationToken.None));
        await Gateway.DidNotReceive().RefundCaptureAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<decimal?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TwoDistinctKeys_BothRefund()
    {
        var (order, payment) = ArrangeCaptured(31.50m);
        Gateway.RefundCaptureAsync(Arg.Any<string>(), "CAP1", 10m, "USD", Arg.Any<CancellationToken>())
            .Returns(new RefundResult("PPR-A", "COMPLETED", 10m, 10m));
        Gateway.RefundCaptureAsync(Arg.Any<string>(), "CAP1", 5m, "USD", Arg.Any<CancellationToken>())
            .Returns(new RefundResult("PPR-B", "COMPLETED", 5m, 15m));

        await CreateService().RefundAsync(OrderId, BuyerId, 10m, "ref-A", CancellationToken.None);
        var second = await CreateService().RefundAsync(OrderId, BuyerId, 5m, "ref-B", CancellationToken.None);

        Assert.Equal("PPR-B", second.PayPalRefundId);
        Assert.Equal(16.50m, second.RemainingRefundable);
    }

    [Fact]
    public async Task NotFulfilled_Throws409()
    {
        var order = NewOrder();
        order.MarkAuthorized();
        OrderRepo.GetByIdAsync(OrderId, Arg.Any<CancellationToken>()).Returns(order);
        PaymentRepo.FirstOrDefaultAsync(Arg.Any<OrderPaymentByOrderIdSpecification>(), Arg.Any<CancellationToken>()).Returns(PaymentFor(31.50m));

        await Assert.ThrowsAsync<OrderStateConflictException>(() =>
            CreateService().RefundAsync(OrderId, BuyerId, 5m, "ref-A", CancellationToken.None));
    }

    [Fact]
    public async Task NotOwned_Throws404()
    {
        ArrangeCaptured();
        await Assert.ThrowsAsync<OrderNotFoundException>(() =>
            CreateService().RefundAsync(OrderId, OtherBuyerId, 5m, "ref-A", CancellationToken.None));
    }
}
