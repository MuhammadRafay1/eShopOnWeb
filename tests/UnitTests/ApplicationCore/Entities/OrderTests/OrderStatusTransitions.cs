using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.OrderTests;

public class OrderStatusTransitions
{
    private static Order NewOrder()
    {
        var address = new Address("1 St", "City", "ST", "US", "00000");
        var items = new List<OrderItem>
        {
            new OrderItem(new CatalogItemOrdered(1, "Item", "pic.png"), 10m, 1)
        };
        return new Order("buyer@example.com", address, items);
    }

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
    public void CannotFulfilBeforeAuthorization()
    {
        var order = NewOrder();
        Assert.Throws<InvalidOperationException>(() => order.MarkFulfilled());
    }

    [Fact]
    public void CannotAuthorizeTwice()
    {
        var order = NewOrder();
        order.MarkPaymentAuthorized();
        Assert.Throws<InvalidOperationException>(() => order.MarkPaymentAuthorized());
    }

    [Fact]
    public void CanCancelWhileAwaitingOrAuthorized()
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
        Assert.Throws<InvalidOperationException>(() => order.MarkCancelled());
    }

    [Fact]
    public void RefundOnlyFromFulfilledOrPartiallyRefunded()
    {
        var order = NewOrder();
        order.MarkPaymentAuthorized();
        order.MarkFulfilled();

        order.MarkPartiallyRefunded();
        Assert.Equal(OrderStatus.PartiallyRefunded, order.Status);

        order.MarkRefunded();
        Assert.Equal(OrderStatus.Refunded, order.Status);
    }

    [Fact]
    public void CannotRefundBeforeFulfilment()
    {
        var order = NewOrder();
        order.MarkPaymentAuthorized();
        Assert.Throws<InvalidOperationException>(() => order.MarkPartiallyRefunded());
        Assert.Throws<InvalidOperationException>(() => order.MarkRefunded());
    }
}
