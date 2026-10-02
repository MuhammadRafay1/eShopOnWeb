using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.UnitTests.Builders;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.OrderTests;

public class OrderPaymentLifecycle
{
    [Fact]
    public void BeginAuthorizationMovesToAuthorizingAndCreatesPayment()
    {
        var order = new OrderBuilder().WithDefaultValues();

        var payment = order.BeginAuthorization("USD", "Order payment");

        Assert.Equal(OrderStatus.Authorizing, order.Status);
        Assert.Same(payment, order.Payment);
        Assert.Equal(order.Total(), payment.Amount);
    }

    [Fact]
    public void CannotBeginAuthorizationTwiceWhileAlreadyInFlight()
    {
        var order = new OrderBuilder().WithDefaultValues();
        order.BeginAuthorization("USD", "Order payment");

        Assert.Throws<InvalidOperationException>(() => order.BeginAuthorization("USD", "Order payment"));
    }

    [Fact]
    public void RecordAuthorizedMovesToAuthorized()
    {
        var order = new OrderBuilder().WithDefaultValues();
        order.BeginAuthorization("USD", "Order payment");

        order.RecordAuthorized("paypal-order-1", "auth-1", "CREATED");

        Assert.Equal(OrderStatus.Authorized, order.Status);
        Assert.Equal("auth-1", order.Payment!.AuthorizationId);
    }

    [Fact]
    public void FailedAuthorizationCanBeRetriedWithAFreshIdempotencySalt()
    {
        var order = new OrderBuilder().WithDefaultValues();
        order.BeginAuthorization("USD", "Order payment");
        var firstSalt = order.Payment!.IdempotencySalt;
        order.RecordAuthorizationFailed("declined");

        var payment = order.BeginAuthorization("USD", "Order payment");

        Assert.Equal(OrderStatus.Authorizing, order.Status);
        Assert.Same(payment, order.Payment);
        Assert.NotEqual(firstSalt, order.Payment!.IdempotencySalt);
    }

    [Fact]
    public void CannotFulfilAnOrderThatWasNeverAuthorized()
    {
        var order = new OrderBuilder().WithDefaultValues();

        Assert.Throws<InvalidOperationException>(() => order.BeginCapture());
    }

    [Fact]
    public void FulfilmentRecordsCaptureDetailsAndMovesToFulfilled()
    {
        var order = new OrderBuilder().WithDefaultValues();
        order.BeginAuthorization("USD", "Order payment");
        order.RecordAuthorized("paypal-order-1", "auth-1", "CREATED");
        order.BeginCapture();

        order.RecordFulfilled("capture-1", "COMPLETED", order.Total(), 1.50m, order.Total() - 1.50m);

        Assert.Equal(OrderStatus.Fulfilled, order.Status);
        Assert.Equal("capture-1", order.Payment!.CaptureId);
        Assert.Equal(1.50m, order.Payment.PayPalFee);
        Assert.NotNull(order.Payment.CapturedAt);
    }

    [Fact]
    public void FailedFulfilmentLandsBackOnAuthorizedSoItCanBeRetried()
    {
        var order = new OrderBuilder().WithDefaultValues();
        order.BeginAuthorization("USD", "Order payment");
        order.RecordAuthorized("paypal-order-1", "auth-1", "CREATED");
        order.BeginCapture();

        order.RecordFulfilmentFailed("authorization stale");

        Assert.Equal(OrderStatus.Authorized, order.Status);
        order.BeginCapture(); // must not throw — fulfilment is retryable
    }

    [Fact]
    public void CancelReleasesTheHoldAndNeverCapturesAnything()
    {
        var order = new OrderBuilder().WithDefaultValues();
        order.BeginAuthorization("USD", "Order payment");
        order.RecordAuthorized("paypal-order-1", "auth-1", "CREATED");

        order.BeginCancel();
        order.RecordCancelled();

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Null(order.Payment!.CaptureId);
        Assert.Equal("VOIDED", order.Payment.AuthorizationStatus);
    }

    [Fact]
    public void CannotCancelAFulfilledOrder()
    {
        var order = new OrderBuilder().WithDefaultValues();
        order.BeginAuthorization("USD", "Order payment");
        order.RecordAuthorized("paypal-order-1", "auth-1", "CREATED");
        order.BeginCapture();
        order.RecordFulfilled("capture-1", "COMPLETED", order.Total(), 1m, order.Total() - 1m);

        Assert.Throws<InvalidOperationException>(() => order.BeginCancel());
    }

    [Fact]
    public void PartialRefundLeavesOrderPartiallyRefundedAndTracksRemainingRefundable()
    {
        var order = FulfilledOrder(out var capturedAmount);

        order.BeginRefund();
        var refund = order.Payment!.AddRefundClaim("key-1", 1m);
        order.RecordRefunded(refund, "refund-1", "COMPLETED", 1m);

        Assert.Equal(OrderStatus.PartiallyRefunded, order.Status);
        Assert.Equal(capturedAmount - 1m, order.Payment.RemainingRefundable());
    }

    [Fact]
    public void FullyRefundingTheCapturedAmountMovesToRefunded()
    {
        var order = FulfilledOrder(out var capturedAmount);

        order.BeginRefund();
        var refund = order.Payment!.AddRefundClaim("key-1", capturedAmount);
        order.RecordRefunded(refund, "refund-1", "COMPLETED", capturedAmount);

        Assert.Equal(OrderStatus.Refunded, order.Status);
        Assert.Equal(0m, order.Payment.RemainingRefundable());
    }

    [Fact]
    public void RepeatingARefundRequestUnderTheSameIdempotencyKeyReturnsTheOriginalRefundInsteadOfANewOne()
    {
        var order = FulfilledOrder(out _);
        order.BeginRefund();
        var firstAttempt = order.Payment!.AddRefundClaim("caller-key", 1m);
        order.RecordRefunded(firstAttempt, "refund-1", "COMPLETED", 1m);

        var replay = order.Payment.FindRefund("caller-key");

        Assert.Same(firstAttempt, replay);
        Assert.Equal("refund-1", replay!.PayPalRefundId);
    }

    [Fact]
    public void TwoDistinctPartialRefundsWithDifferentKeysAreBothLegitimate()
    {
        var order = FulfilledOrder(out var capturedAmount);
        order.BeginRefund();
        var first = order.Payment!.AddRefundClaim("key-1", 1m);
        order.RecordRefunded(first, "refund-1", "COMPLETED", 1m);

        order.BeginRefund();
        var second = order.Payment!.AddRefundClaim("key-2", 2m);
        order.RecordRefunded(second, "refund-2", "COMPLETED", 2m);

        Assert.Equal(2, order.Payment.Refunds.Count);
        Assert.Equal(capturedAmount - 3m, order.Payment.RemainingRefundable());
    }

    private static Order FulfilledOrder(out decimal capturedAmount)
    {
        var order = new OrderBuilder().WithDefaultValues();
        order.BeginAuthorization("USD", "Order payment");
        order.RecordAuthorized("paypal-order-1", "auth-1", "CREATED");
        order.BeginCapture();
        capturedAmount = order.Total();
        order.RecordFulfilled("capture-1", "COMPLETED", capturedAmount, 1m, capturedAmount - 1m);
        return order;
    }
}
