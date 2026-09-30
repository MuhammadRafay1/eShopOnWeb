using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.UnitTests.Builders;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.OrderTests;

public class OrderStatusTransitions
{
    [Fact]
    public void StartsAwaitingPayment()
    {
        var order = new OrderBuilder().WithDefaultValues();
        Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
    }

    [Fact]
    public void MarkAuthorizedMovesToAuthorized()
    {
        var order = new OrderBuilder().WithDefaultValues();
        order.MarkAuthorized();
        Assert.Equal(OrderStatus.Authorized, order.Status);
    }

    [Fact]
    public void MarkAuthorizedTwiceThrows()
    {
        var order = new OrderBuilder().WithDefaultValues();
        order.MarkAuthorized();
        Assert.Throws<InvalidOrderStateException>(() => order.MarkAuthorized());
    }

    [Fact]
    public void MarkFulfilledRequiresAuthorizedFirst()
    {
        var order = new OrderBuilder().WithDefaultValues();
        Assert.Throws<InvalidOrderStateException>(() => order.MarkFulfilled());
    }

    [Fact]
    public void MarkFulfilledAfterAuthorizedSucceeds()
    {
        var order = new OrderBuilder().WithDefaultValues();
        order.MarkAuthorized();
        order.MarkFulfilled();
        Assert.Equal(OrderStatus.Fulfilled, order.Status);
    }

    [Fact]
    public void CannotCancelAfterFulfilled()
    {
        var order = new OrderBuilder().WithDefaultValues();
        order.MarkAuthorized();
        order.MarkFulfilled();
        Assert.Throws<InvalidOrderStateException>(() => order.MarkCancelled());
    }

    [Fact]
    public void CancelRequiresAuthorizedFirst()
    {
        var order = new OrderBuilder().WithDefaultValues();
        Assert.Throws<InvalidOrderStateException>(() => order.MarkCancelled());
    }

    [Fact]
    public void CancelAfterAuthorizedSucceeds()
    {
        var order = new OrderBuilder().WithDefaultValues();
        order.MarkAuthorized();
        order.MarkCancelled();
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void PartiallyRefundedThenFullyRefundedSucceeds()
    {
        var order = new OrderBuilder().WithDefaultValues();
        order.MarkAuthorized();
        order.MarkFulfilled();
        order.MarkPartiallyRefunded();
        Assert.Equal(OrderStatus.PartiallyRefunded, order.Status);

        order.MarkRefunded();
        Assert.Equal(OrderStatus.Refunded, order.Status);
    }

    [Fact]
    public void RefundRequiresFulfilledFirst()
    {
        var order = new OrderBuilder().WithDefaultValues();
        order.MarkAuthorized();
        Assert.Throws<InvalidOrderStateException>(() => order.MarkRefunded());
    }
}
