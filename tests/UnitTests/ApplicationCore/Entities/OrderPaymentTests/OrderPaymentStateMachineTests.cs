using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderPaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.OrderPaymentTests;

public class OrderPaymentStateMachineTests
{
    private static OrderPayment NewPayment() => new(orderId: 1, buyerId: "buyer@example.com", currencyCode: "USD");

    [Fact]
    public void StartsAwaitingPaymentWithGeneratedIdempotencyKeys()
    {
        var payment = NewPayment();

        Assert.Equal(OrderPaymentStatus.AwaitingPayment, payment.Status);
        Assert.False(string.IsNullOrWhiteSpace(payment.AuthorizeRequestId));
        Assert.False(string.IsNullOrWhiteSpace(payment.CaptureRequestId));
        Assert.False(string.IsNullOrWhiteSpace(payment.VoidRequestId));
        Assert.NotEqual(payment.AuthorizeRequestId, payment.CaptureRequestId);
    }

    [Fact]
    public void MarkAuthorized_FromAwaitingPayment_Succeeds()
    {
        var payment = NewPayment();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(3);

        payment.MarkAuthorized("PP-ORDER-1", "AUTH-1", expiresAt, 42.50m);

        Assert.Equal(OrderPaymentStatus.Authorized, payment.Status);
        Assert.Equal("PP-ORDER-1", payment.PayPalOrderId);
        Assert.Equal("AUTH-1", payment.AuthorizationId);
        Assert.Equal(expiresAt, payment.AuthorizationExpiresAt);
        Assert.Equal(42.50m, payment.AuthorizedAmount);
        Assert.NotNull(payment.AuthorizedAt);
    }

    [Fact]
    public void MarkAuthorized_WhenAlreadyAuthorized_Throws()
    {
        var payment = NewPayment();
        payment.MarkAuthorized("PP-ORDER-1", "AUTH-1", null, 10m);

        Assert.Throws<OrderPaymentStateException>(() => payment.MarkAuthorized("PP-ORDER-2", "AUTH-2", null, 10m));
    }

    [Fact]
    public void MarkCaptured_RequiresAuthorized()
    {
        var payment = NewPayment();

        Assert.Throws<OrderPaymentStateException>(() => payment.MarkCaptured("CAP-1", 10m, 1m, 9m));
    }

    [Fact]
    public void MarkCaptured_FromAuthorized_Succeeds()
    {
        var payment = NewPayment();
        payment.MarkAuthorized("PP-ORDER-1", "AUTH-1", null, 10m);

        payment.MarkCaptured("CAP-1", 10m, 0.59m, 9.41m);

        Assert.Equal(OrderPaymentStatus.Captured, payment.Status);
        Assert.Equal("CAP-1", payment.CaptureId);
        Assert.Equal(10m, payment.CapturedGrossAmount);
        Assert.Equal(0.59m, payment.PayPalFee);
        Assert.Equal(9.41m, payment.NetAmount);
        Assert.NotNull(payment.CapturedAt);
    }

    [Fact]
    public void MarkCancelled_RequiresAuthorized()
    {
        var payment = NewPayment();
        Assert.Throws<OrderPaymentStateException>(() => payment.MarkCancelled());
    }

    [Fact]
    public void MarkCancelled_FromAuthorized_Succeeds()
    {
        var payment = NewPayment();
        payment.MarkAuthorized("PP-ORDER-1", "AUTH-1", null, 10m);

        payment.MarkCancelled();

        Assert.Equal(OrderPaymentStatus.Cancelled, payment.Status);
    }

    [Fact]
    public void MarkCancelled_AfterCaptured_Throws()
    {
        var payment = NewPayment();
        payment.MarkAuthorized("PP-ORDER-1", "AUTH-1", null, 10m);
        payment.MarkCaptured("CAP-1", 10m, 1m, 9m);

        Assert.Throws<OrderPaymentStateException>(() => payment.MarkCancelled());
    }

    [Fact]
    public void AddRefund_RequiresCaptured()
    {
        var payment = NewPayment();
        Assert.Throws<OrderPaymentStateException>(() => payment.AddRefund("REF-1", 1m, "key-1", "COMPLETED"));
    }

    [Fact]
    public void AddRefund_FullAmount_MarksRefunded()
    {
        var payment = NewPayment();
        payment.MarkAuthorized("PP-ORDER-1", "AUTH-1", null, 10m);
        payment.MarkCaptured("CAP-1", 10m, 1m, 9m);

        payment.AddRefund("REF-1", 10m, "key-1", "COMPLETED");

        Assert.Equal(OrderPaymentStatus.Refunded, payment.Status);
        Assert.Equal(10m, payment.TotalRefundedAmount);
        Assert.Single(payment.Refunds);
    }

    [Fact]
    public void AddRefund_PartialAmount_MarksPartiallyRefunded_AndAllowsAnotherPartial()
    {
        var payment = NewPayment();
        payment.MarkAuthorized("PP-ORDER-1", "AUTH-1", null, 10m);
        payment.MarkCaptured("CAP-1", 10m, 1m, 9m);

        payment.AddRefund("REF-1", 4m, "key-1", "COMPLETED");
        Assert.Equal(OrderPaymentStatus.PartiallyRefunded, payment.Status);
        Assert.Equal(4m, payment.TotalRefundedAmount);

        payment.AddRefund("REF-2", 6m, "key-2", "COMPLETED");
        Assert.Equal(OrderPaymentStatus.Refunded, payment.Status);
        Assert.Equal(10m, payment.TotalRefundedAmount);
        Assert.Equal(2, payment.Refunds.Count);
    }

    [Fact]
    public void AddRefund_ExceedingCapturedAmount_Throws()
    {
        var payment = NewPayment();
        payment.MarkAuthorized("PP-ORDER-1", "AUTH-1", null, 10m);
        payment.MarkCaptured("CAP-1", 10m, 1m, 9m);
        payment.AddRefund("REF-1", 7m, "key-1", "COMPLETED");

        // A second refund that would push the total past what was captured must never be allowed,
        // even though each individual refund amount is itself valid.
        Assert.Throws<RefundAmountExceededException>(() => payment.AddRefund("REF-2", 4m, "key-2", "COMPLETED"));
        Assert.Equal(7m, payment.TotalRefundedAmount);
        Assert.Equal(OrderPaymentStatus.PartiallyRefunded, payment.Status);
    }

    [Fact]
    public void RenewAuthorization_RequiresAuthorized()
    {
        var payment = NewPayment();
        Assert.Throws<OrderPaymentStateException>(() => payment.RenewAuthorization("AUTH-2", null));
    }

    [Fact]
    public void RenewAuthorization_UpdatesAuthorizationIdAndExpiry()
    {
        var payment = NewPayment();
        payment.MarkAuthorized("PP-ORDER-1", "AUTH-1", DateTimeOffset.UtcNow, 10m);
        var newExpiry = DateTimeOffset.UtcNow.AddDays(3);

        payment.RenewAuthorization("AUTH-2", newExpiry);

        Assert.Equal("AUTH-2", payment.AuthorizationId);
        Assert.Equal(newExpiry, payment.AuthorizationExpiresAt);
        Assert.Equal(OrderPaymentStatus.Authorized, payment.Status);
    }
}
