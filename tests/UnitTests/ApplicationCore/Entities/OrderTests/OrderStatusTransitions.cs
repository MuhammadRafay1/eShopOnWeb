using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.UnitTests.Builders;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.OrderTests;

public class OrderStatusTransitions
{
    private Order NewOrder() => new OrderBuilder().WithDefaultValues();

    [Fact]
    public void NewOrderIsAwaitingPayment()
    {
        Assert.Equal(OrderStatus.AwaitingPayment, NewOrder().Status);
    }

    [Fact]
    public void CanAuthorizeAnAwaitingPaymentOrder()
    {
        var order = NewOrder();
        order.MarkPaymentAuthorized();
        Assert.Equal(OrderStatus.PaymentAuthorized, order.Status);
    }

    [Fact]
    public void CannotFulfilAnUnauthorizedOrder()
    {
        var order = NewOrder();
        Assert.ThrowsAny<Exception>(() => order.MarkFulfilled());
    }

    [Fact]
    public void CanFulfilAnAuthorizedOrder()
    {
        var order = NewOrder();
        order.MarkPaymentAuthorized();
        order.MarkFulfilled();
        Assert.Equal(OrderStatus.Fulfilled, order.Status);
    }

    [Fact]
    public void CannotCancelAFulfilledOrder()
    {
        var order = NewOrder();
        order.MarkPaymentAuthorized();
        order.MarkFulfilled();
        Assert.ThrowsAny<Exception>(() => order.MarkCancelled());
    }

    [Fact]
    public void CanCancelAnAuthorizedOrder()
    {
        var order = NewOrder();
        order.MarkPaymentAuthorized();
        order.MarkCancelled();
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void CannotRefundAnUnfulfilledOrder()
    {
        var order = NewOrder();
        order.MarkPaymentAuthorized();
        Assert.ThrowsAny<Exception>(() => order.MarkPartiallyRefunded());
    }

    [Fact]
    public void CanPartiallyThenFullyRefundAFulfilledOrder()
    {
        var order = NewOrder();
        order.MarkPaymentAuthorized();
        order.MarkFulfilled();
        order.MarkPartiallyRefunded();
        Assert.Equal(OrderStatus.PartiallyRefunded, order.Status);
        order.MarkFullyRefunded();
        Assert.Equal(OrderStatus.Refunded, order.Status);
    }

    [Fact]
    public void CannotAuthorizeTwice()
    {
        var order = NewOrder();
        order.MarkPaymentAuthorized();
        Assert.ThrowsAny<Exception>(() => order.MarkPaymentAuthorized());
    }
}
