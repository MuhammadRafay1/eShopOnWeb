using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using System;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.PaymentTests;

public class PaymentBehavior
{
    private static Payment AuthorizedCapturedPayment(decimal gross = 100m)
    {
        var payment = new Payment(orderId: 1, authorizedAmount: gross, currencyCode: "USD");
        payment.RecordAuthorization("PPO-1", "AUTH-1", PaymentAuthorizationStatus.Created,
            DateTimeOffset.UtcNow.AddDays(29), "inv-1");
        payment.RecordCapture("CAP-1", PaymentCaptureStatus.Completed, gross, 3m, gross - 3m);
        return payment;
    }

    [Fact]
    public void IncrementPaymentAttemptCounts()
    {
        var payment = new Payment(1, 10m, "USD");
        Assert.Equal(1, payment.IncrementPaymentAttempt());
        Assert.Equal(2, payment.IncrementPaymentAttempt());
    }

    [Fact]
    public void PartialRefundsAccumulateAndTrackRemaining()
    {
        var payment = AuthorizedCapturedPayment(100m);

        payment.AddRefund("R-1", 30m, "COMPLETED", "key-1");
        Assert.Equal(30m, payment.RefundedAmount);
        Assert.Equal(70m, payment.RemainingRefundable);
        Assert.Equal(PaymentCaptureStatus.PartiallyRefunded, payment.CaptureStatus);

        payment.AddRefund("R-2", 70m, "COMPLETED", "key-2");
        Assert.Equal(100m, payment.RefundedAmount);
        Assert.Equal(0m, payment.RemainingRefundable);
        Assert.Equal(PaymentCaptureStatus.Refunded, payment.CaptureStatus);
    }

    [Fact]
    public void RefundBeyondCapturedThrows()
    {
        var payment = AuthorizedCapturedPayment(100m);
        payment.AddRefund("R-1", 80m, "COMPLETED", "key-1");

        Assert.Throws<RefundLimitExceededException>(
            () => payment.AddRefund("R-2", 30m, "COMPLETED", "key-2"));
    }

    [Fact]
    public void RefundBeforeCaptureThrows()
    {
        var payment = new Payment(1, 100m, "USD");
        Assert.Throws<InvalidOrderStateException>(
            () => payment.AddRefund("R-1", 10m, "COMPLETED", "key-1"));
    }

    [Fact]
    public void FindRefundByIdempotencyKeyReturnsStored()
    {
        var payment = AuthorizedCapturedPayment(100m);
        var refund = payment.AddRefund("R-1", 25m, "COMPLETED", "idem-123");

        Assert.Same(refund, payment.FindRefundByIdempotencyKey("idem-123"));
        Assert.Null(payment.FindRefundByIdempotencyKey("other"));
    }

    [Fact]
    public void ReauthorizationBumpsCountAndExpiry()
    {
        var payment = new Payment(1, 100m, "USD");
        var firstExpiry = DateTimeOffset.UtcNow.AddDays(1);
        payment.RecordAuthorization("PPO-1", "AUTH-1", PaymentAuthorizationStatus.Created, firstExpiry, "inv-1");

        var newExpiry = DateTimeOffset.UtcNow.AddDays(3);
        payment.RecordReauthorization(PaymentAuthorizationStatus.Created, newExpiry);

        Assert.Equal(1, payment.ReauthorizationCount);
        Assert.Equal(newExpiry, payment.AuthorizationExpiresAt);
    }
}
