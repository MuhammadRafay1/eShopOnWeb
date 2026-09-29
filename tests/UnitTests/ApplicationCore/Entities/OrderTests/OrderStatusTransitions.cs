using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.OrderTests;

public class OrderStatusTransitions
{
    private static Order NewOrder()
    {
        var items = new List<OrderItem>
        {
            new OrderItem(new CatalogItemOrdered(1, "Item", "pic.png"), 10m, 2)
        };
        return new Order("buyer@x.com", new Address("s", "c", "st", "US", "12345"), items);
    }

    private static OrderPayment AuthorizedPayment()
    {
        var p = new OrderPayment("PPO1", "USD", 20m);
        p.RecordAuthorization("AUTH1", "CREATED", DateTimeOffset.UtcNow.AddDays(3));
        return p;
    }

    [Fact]
    public void NewOrderStartsAwaitingPayment()
    {
        Assert.Equal(OrderStatus.AwaitingPayment, NewOrder().Status);
    }

    [Fact]
    public void BeginPaymentMovesToAuthorizedAndIsIdempotent()
    {
        var order = NewOrder();
        order.BeginPayment(AuthorizedPayment());
        Assert.Equal(OrderStatus.Authorized, order.Status);

        // A second BeginPayment (e.g. concurrent double-click) is a no-op — no re-authorize.
        var second = AuthorizedPayment();
        order.BeginPayment(second);
        Assert.Equal(OrderStatus.Authorized, order.Status);
        Assert.NotSame(second, order.Payment);
    }

    [Fact]
    public void CannotFulfilBeforeAuthorized()
    {
        var order = NewOrder();
        Assert.Throws<InvalidOperationException>(() => order.MarkFulfilled());
    }

    [Fact]
    public void FulfilThenCannotFulfilAgain()
    {
        var order = NewOrder();
        order.BeginPayment(AuthorizedPayment());
        order.MarkFulfilled();
        Assert.Equal(OrderStatus.Fulfilled, order.Status);
        Assert.Throws<InvalidOperationException>(() => order.MarkFulfilled());
    }

    [Fact]
    public void CannotCancelAfterFulfilment()
    {
        var order = NewOrder();
        order.BeginPayment(AuthorizedPayment());
        order.MarkFulfilled();
        Assert.Throws<InvalidOperationException>(() => order.MarkCancelled());
    }

    [Fact]
    public void CanCancelBeforeAndAfterAuthorization()
    {
        var awaiting = NewOrder();
        awaiting.MarkCancelled();
        Assert.Equal(OrderStatus.Cancelled, awaiting.Status);

        var authorized = NewOrder();
        authorized.BeginPayment(AuthorizedPayment());
        authorized.MarkCancelled();
        Assert.Equal(OrderStatus.Cancelled, authorized.Status);
    }

    [Fact]
    public void ApplyRefundRequiresFulfilment()
    {
        var order = NewOrder();
        order.BeginPayment(AuthorizedPayment());
        Assert.Throws<InvalidOperationException>(() => order.ApplyRefund(isFullRefund: true));
    }

    [Fact]
    public void ApplyRefundPartialThenFull()
    {
        var order = NewOrder();
        order.BeginPayment(AuthorizedPayment());
        order.MarkFulfilled();

        order.ApplyRefund(isFullRefund: false);
        Assert.Equal(OrderStatus.PartiallyRefunded, order.Status);

        order.ApplyRefund(isFullRefund: true);
        Assert.Equal(OrderStatus.Refunded, order.Status);
    }
}
