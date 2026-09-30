using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.UnitTests.Builders;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.OrderTests;

public class OrderStateTransitions
{
    private static Payment NewPayment() =>
        new Payment("USD", "PPORDER1", "AUTH1", "CREATED", 29m,
            DateTimeOffset.UtcNow.AddDays(29), null);

    private static Order NewOrder() => new OrderBuilder().WithDefaultValues();

    [Fact]
    public void NewOrderStartsAwaitingPayment()
    {
        Assert.Equal(OrderStatus.AwaitingPayment, NewOrder().Status);
    }

    [Fact]
    public void AuthorizingMovesToPaymentAuthorizedAndAttachesPayment()
    {
        var order = NewOrder();
        var payment = NewPayment();

        order.MarkPaymentAuthorized(payment);

        Assert.Equal(OrderStatus.PaymentAuthorized, order.Status);
        Assert.Same(payment, order.Payment);
    }

    [Fact]
    public void CannotFulfilBeforeAuthorization()
    {
        Assert.Throws<OrderStateException>(() => NewOrder().MarkFulfilled());
    }

    [Fact]
    public void CannotAuthorizeTwice()
    {
        var order = NewOrder();
        order.MarkPaymentAuthorized(NewPayment());
        Assert.Throws<OrderStateException>(() => order.MarkPaymentAuthorized(NewPayment()));
    }

    [Fact]
    public void FulfilMovesAuthorizedOrderToFulfilled()
    {
        var order = NewOrder();
        order.MarkPaymentAuthorized(NewPayment());
        order.MarkFulfilled();
        Assert.Equal(OrderStatus.Fulfilled, order.Status);
    }

    [Fact]
    public void CannotCancelAfterFulfilment()
    {
        var order = NewOrder();
        order.MarkPaymentAuthorized(NewPayment());
        order.MarkFulfilled();
        Assert.Throws<OrderStateException>(() => order.MarkCancelled());
    }

    [Fact]
    public void CanCancelBeforeAndAfterAuthorization()
    {
        var awaiting = NewOrder();
        awaiting.MarkCancelled();
        Assert.Equal(OrderStatus.Cancelled, awaiting.Status);

        var authorized = NewOrder();
        authorized.MarkPaymentAuthorized(NewPayment());
        authorized.MarkCancelled();
        Assert.Equal(OrderStatus.Cancelled, authorized.Status);
    }
}
