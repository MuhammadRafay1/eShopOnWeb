using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.PaymentTests;

public class PaymentRefundInvariants
{
    private static Payment CapturedPayment(decimal capturedAmount = 100m)
    {
        var payment = new Payment(orderId: 1, buyerId: "buyer@test.com", currency: "USD", authorizedAmount: capturedAmount);
        payment.RecordAuthorization("ppOrder", "auth1", "CREATED", null, "VISA", "1111");
        payment.RecordCapture("capture1", "COMPLETED", capturedAmount, 5m, capturedAmount - 5m);
        return payment;
    }

    [Fact]
    public void CanRefundIsTrueUpToCapturedAmount()
    {
        var payment = CapturedPayment(100m);
        Assert.True(payment.CanRefund(100m));
        Assert.True(payment.CanRefund(1m));
    }

    [Fact]
    public void CanRefundIsFalseAboveCapturedAmount()
    {
        var payment = CapturedPayment(100m);
        Assert.False(payment.CanRefund(100.01m));
    }

    [Fact]
    public void AddRefundReducesRemainingRefundable()
    {
        var payment = CapturedPayment(100m);
        payment.AddRefund("refund1", 30m, "COMPLETED", "key1", null);

        Assert.Equal(30m, payment.TotalRefunded());
        Assert.True(payment.CanRefund(70m));
        Assert.False(payment.CanRefund(70.01m));
    }

    [Fact]
    public void CumulativeRefundsCannotExceedCapturedAmount()
    {
        var payment = CapturedPayment(100m);
        payment.AddRefund("refund1", 60m, "COMPLETED", "key1", null);

        Assert.Throws<RefundExceedsCaptureException>(() => payment.AddRefund("refund2", 50m, "COMPLETED", "key2", null));
    }

    [Fact]
    public void TwoDistinctPartialRefundsThatFitAreBothLegitimate()
    {
        var payment = CapturedPayment(100m);
        payment.AddRefund("refund1", 40m, "COMPLETED", "key1", null);
        payment.AddRefund("refund2", 60m, "COMPLETED", "key2", null);

        Assert.Equal(100m, payment.TotalRefunded());
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
    }

    [Fact]
    public void PartialRefundLeavesStatusPartiallyRefunded()
    {
        var payment = CapturedPayment(100m);
        payment.AddRefund("refund1", 40m, "COMPLETED", "key1", null);

        Assert.Equal(PaymentStatus.PartiallyRefunded, payment.Status);
    }

    [Fact]
    public void FindRefundByIdempotencyKeyReturnsTheOriginalRefund()
    {
        var payment = CapturedPayment(100m);
        var refund = payment.AddRefund("refund1", 40m, "COMPLETED", "key1", null);

        var found = payment.FindRefundByIdempotencyKey("key1");

        Assert.Same(refund, found);
    }

    [Fact]
    public void FindRefundByIdempotencyKeyReturnsNullForUnknownKey()
    {
        var payment = CapturedPayment(100m);
        payment.AddRefund("refund1", 40m, "COMPLETED", "key1", null);

        Assert.Null(payment.FindRefundByIdempotencyKey("unknown-key"));
    }
}
