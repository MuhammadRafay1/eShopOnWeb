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
    public void HappyPathAuthorizeThenFulfil()
    {
        var order = NewOrder();
        order.MarkPaymentAuthorized();
        Assert.Equal(OrderStatus.PaymentAuthorized, order.Status);

        order.MarkFulfilled();
        Assert.Equal(OrderStatus.Fulfilled, order.Status);
    }

    [Fact]
    public void CannotFulfilBeforeAuthorized()
    {
        var order = NewOrder();
        Assert.Throws<InvalidOrderStateException>(() => order.MarkFulfilled());
    }

    [Fact]
    public void CannotAuthorizeTwice()
    {
        var order = NewOrder();
        order.MarkPaymentAuthorized();
        Assert.Throws<InvalidOrderStateException>(() => order.MarkPaymentAuthorized());
    }

    [Fact]
    public void CanCancelBeforeAndAfterAuthorization()
    {
        var awaiting = NewOrder();
        awaiting.MarkCancelled();
        Assert.Equal(OrderStatus.Cancelled, awaiting.Status);

        var authorized = NewOrder();
        authorized.MarkPaymentAuthorized();
        authorized.MarkCancelled();
        Assert.Equal(OrderStatus.Cancelled, authorized.Status);
    }

    [Fact]
    public void CannotCancelAfterFulfilment()
    {
        var order = NewOrder();
        order.MarkPaymentAuthorized();
        order.MarkFulfilled();
        Assert.Throws<InvalidOrderStateException>(() => order.MarkCancelled());
    }

    [Fact]
    public void RefundRequiresFulfilment()
    {
        var order = NewOrder();
        order.MarkPaymentAuthorized();
        Assert.Throws<InvalidOrderStateException>(() => order.MarkRefunded(partial: false));
    }

    [Fact]
    public void PartialThenFullRefundTransitions()
    {
        var order = NewOrder();
        order.MarkPaymentAuthorized();
        order.MarkFulfilled();

        order.MarkRefunded(partial: true);
        Assert.Equal(OrderStatus.PartiallyRefunded, order.Status);

        order.MarkRefunded(partial: false);
        Assert.Equal(OrderStatus.Refunded, order.Status);
    }
}
