using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PaymentGateway;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.OrderPaymentServiceTests;

public class FulfilTests : OrderPaymentServiceTestBase
{
    private (Order order, OrderPayment payment) ArrangeAuthorized(DateTimeOffset? expiresAt = null)
    {
        var order = NewOrder();
        order.MarkAuthorized();
        var payment = PaymentFor(order.Total(), expiresAt);
        OrderRepo.GetByIdAsync(OrderId, Arg.Any<CancellationToken>()).Returns(order);
        PaymentRepo.FirstOrDefaultAsync(Arg.Any<OrderPaymentByOrderIdSpecification>(), Arg.Any<CancellationToken>()).Returns(payment);
        return (order, payment);
    }

    [Fact]
    public async Task Capture_MarksFulfilled_WithFeeAndNet()
    {
        var (order, payment) = ArrangeAuthorized();
        Gateway.CaptureAuthorizationAsync(Arg.Any<string>(), "AUTH1", Arg.Any<CancellationToken>())
            .Returns(new CaptureResult("CAP1", "COMPLETED", order.Total(), 1.31m, order.Total() - 1.31m));

        var result = await CreateService().FulfilAsync(OrderId, CancellationToken.None);

        Assert.Equal(OrderStatus.Fulfilled, result.Status);
        Assert.Equal(order.Total(), result.CapturedAmount);
        Assert.Equal(1.31m, result.PayPalFee);
        Assert.Equal(OrderStatus.Fulfilled, order.Status);
        Assert.Equal("CAP1", payment.PayPalCaptureId);
    }

    [Fact]
    public async Task WrongState_Throws409()
    {
        var order = NewOrder(); // AwaitingPayment
        OrderRepo.GetByIdAsync(OrderId, Arg.Any<CancellationToken>()).Returns(order);

        await Assert.ThrowsAsync<OrderStateConflictException>(() =>
            CreateService().FulfilAsync(OrderId, CancellationToken.None));
    }

    [Fact]
    public async Task StaleAuthorization_IsReauthorizedBeforeCapture()
    {
        // Expiry within the safety buffer -> proactive reauthorize.
        var (order, payment) = ArrangeAuthorized(DateTimeOffset.UtcNow.AddSeconds(10));
        Gateway.ReauthorizeAsync(Arg.Any<string>(), "AUTH1", order.Total(), "USD", Arg.Any<CancellationToken>())
            .Returns(new AuthorizationResult("PPO", "AUTH2", "CREATED", order.Total(), DateTimeOffset.UtcNow.AddDays(29), false));
        Gateway.CaptureAuthorizationAsync(Arg.Any<string>(), "AUTH2", Arg.Any<CancellationToken>())
            .Returns(new CaptureResult("CAP2", "COMPLETED", order.Total(), 1m, order.Total() - 1m));

        var result = await CreateService().FulfilAsync(OrderId, CancellationToken.None);

        Assert.Equal(OrderStatus.Fulfilled, result.Status);
        await Gateway.Received(1).ReauthorizeAsync(Arg.Any<string>(), "AUTH1", order.Total(), "USD", Arg.Any<CancellationToken>());
        await Gateway.Received(1).CaptureAuthorizationAsync(Arg.Any<string>(), "AUTH2", Arg.Any<CancellationToken>());
        Assert.Equal("AUTH2", payment.PayPalAuthorizationId);
    }

    [Fact]
    public async Task CaptureFails_RetriesOnceWithReauth()
    {
        var (order, payment) = ArrangeAuthorized();
        Gateway.CaptureAuthorizationAsync(Arg.Any<string>(), "AUTH1", Arg.Any<CancellationToken>())
            .Throws(new PaymentGatewayException("stale"));
        Gateway.ReauthorizeAsync(Arg.Any<string>(), "AUTH1", order.Total(), "USD", Arg.Any<CancellationToken>())
            .Returns(new AuthorizationResult("PPO", "AUTH3", "CREATED", order.Total(), DateTimeOffset.UtcNow.AddDays(29), false));
        Gateway.CaptureAuthorizationAsync(Arg.Any<string>(), "AUTH3", Arg.Any<CancellationToken>())
            .Returns(new CaptureResult("CAP3", "COMPLETED", order.Total(), 1m, order.Total() - 1m));

        var result = await CreateService().FulfilAsync(OrderId, CancellationToken.None);

        Assert.Equal(OrderStatus.Fulfilled, result.Status);
        await Gateway.Received(1).ReauthorizeAsync(Arg.Any<string>(), "AUTH1", order.Total(), "USD", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unrecoverable_RevertsToAwaitingPayment_AndDeletesDeadPayment()
    {
        var (order, payment) = ArrangeAuthorized();
        Gateway.CaptureAuthorizationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new PaymentGatewayException("expired"));
        Gateway.ReauthorizeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new PaymentDeclinedException("cannot reauthorize"));

        await Assert.ThrowsAsync<AuthorizationCannotBeRenewedException>(() =>
            CreateService().FulfilAsync(OrderId, CancellationToken.None));

        Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
        await PaymentRepo.Received(1).DeleteAsync(payment, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AlreadyFulfilled_IsIdempotentNoOp()
    {
        var order = NewOrder();
        order.MarkAuthorized();
        order.MarkFulfilled();
        var payment = PaymentFor(order.Total());
        payment.RecordCapture("CAP1", order.Total(), 1m, order.Total() - 1m);
        OrderRepo.GetByIdAsync(OrderId, Arg.Any<CancellationToken>()).Returns(order);
        PaymentRepo.FirstOrDefaultAsync(Arg.Any<OrderPaymentByOrderIdSpecification>(), Arg.Any<CancellationToken>()).Returns(payment);

        var result = await CreateService().FulfilAsync(OrderId, CancellationToken.None);

        Assert.Equal(OrderStatus.Fulfilled, result.Status);
        await Gateway.DidNotReceive().CaptureAuthorizationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
