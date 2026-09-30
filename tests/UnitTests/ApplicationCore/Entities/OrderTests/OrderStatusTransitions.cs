using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.UnitTests.Builders;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.OrderTests;

public class OrderStatusTransitions
{
    private static Order NewOrder() => new OrderBuilder().WithDefaultValues();

    [Fact]
    public void NewOrderStartsAwaitingPayment()
    {
        Assert.Equal(OrderStatus.AwaitingPayment, NewOrder().Status);
    }

    [Fact]
    public void AuthorizeThenFulfilFollowsHappyPath()
    {
        var order = NewOrder();

        order.MarkPaymentAuthorized();
        Assert.Equal(OrderStatus.PaymentAuthorized, order.Status);

        order.MarkFulfilled();
        Assert.Equal(OrderStatus.Fulfilled, order.Status);
    }

    [Fact]
    public void FulfilBeforeAuthorizeThrows()
    {
        var order = NewOrder();
        Assert.Throws<InvalidOrderStateException>(() => order.MarkFulfilled());
    }

    [Fact]
    public void AuthorizeTwiceIsIdempotentNoOp()
    {
        var order = NewOrder();
        order.MarkPaymentAuthorized();
        order.MarkPaymentAuthorized(); // no throw
        Assert.Equal(OrderStatus.PaymentAuthorized, order.Status);
    }

    [Fact]
    public void CancelAllowedBeforeFulfilment()
    {
        var order = NewOrder();
        order.MarkPaymentAuthorized();
        order.MarkCancelled();
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void CancelAfterFulfilmentThrows()
    {
        var order = NewOrder();
        order.MarkPaymentAuthorized();
        order.MarkFulfilled();
        Assert.Throws<InvalidOrderStateException>(() => order.MarkCancelled());
    }

    [Fact]
    public void PayAfterCancelIsRejectedByCancellableGuard()
    {
        var order = NewOrder();
        order.MarkCancelled();
        Assert.Throws<InvalidOrderStateException>(() => order.MarkPaymentAuthorized());
    }

    [Fact]
    public void PartialThenFullRefundTransitionsCorrectly()
    {
        var order = NewOrder();
        order.MarkPaymentAuthorized();
        order.MarkFulfilled();

        order.MarkRefunded(isFullRefund: false);
        Assert.Equal(OrderStatus.PartiallyRefunded, order.Status);

        order.MarkRefunded(isFullRefund: true);
        Assert.Equal(OrderStatus.Refunded, order.Status);
    }

    [Fact]
    public void RefundBeforeFulfilmentThrows()
    {
        var order = NewOrder();
        order.MarkPaymentAuthorized();
        Assert.Throws<InvalidOrderStateException>(() => order.MarkRefunded(true));
    }

    [Fact]
    public void PredicatesReflectState()
    {
        var order = NewOrder();
        Assert.True(order.CurrentlyPayable);
        Assert.True(order.CurrentlyCancellable);
        Assert.False(order.CurrentlyFulfillable);
        Assert.False(order.CurrentlyRefundable);

        order.MarkPaymentAuthorized();
        Assert.False(order.CurrentlyPayable);
        Assert.True(order.CurrentlyFulfillable);
        Assert.True(order.CurrentlyCancellable);

        order.MarkFulfilled();
        Assert.True(order.CurrentlyRefundable);
        Assert.False(order.CurrentlyCancellable);
    }
}
