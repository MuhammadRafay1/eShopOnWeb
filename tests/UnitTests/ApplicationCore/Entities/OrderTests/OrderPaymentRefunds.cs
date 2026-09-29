using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.OrderTests;

public class OrderPaymentRefunds
{
    private static OrderPayment CapturedPayment(decimal captured = 100m)
    {
        var p = new OrderPayment("PPO1", "USD", captured);
        p.RecordAuthorization("AUTH1", "CREATED", DateTimeOffset.UtcNow.AddDays(3));
        p.RecordCapture("CAP1", "COMPLETED", captured, 3m, captured - 3m);
        return p;
    }

    private static Refund Refund(decimal amount, string key, string status = "COMPLETED")
        => new Refund($"R-{key}", amount, "USD", status, key);

    [Fact]
    public void RecordCaptureStoresBreakdown()
    {
        var p = CapturedPayment(50m);
        Assert.Equal(50m, p.CapturedAmount);
        Assert.Equal(3m, p.PayPalFeeAmount);
        Assert.Equal(47m, p.NetAmount);
        Assert.Equal("CAP1", p.PayPalCaptureId);
    }

    [Fact]
    public void PartialRefundsAccumulateAndTrackRemaining()
    {
        var p = CapturedPayment(100m);
        p.AddRefund(Refund(30m, "K1"));
        p.AddRefund(Refund(20m, "K2"));

        Assert.Equal(50m, p.RefundedAmount);
        Assert.Equal(50m, p.RemainingRefundable);
        Assert.False(p.IsFullyRefunded);
    }

    [Fact]
    public void RefundExceedingCapturedIsRejected()
    {
        var p = CapturedPayment(100m);
        p.AddRefund(Refund(80m, "K1"));
        Assert.Throws<InvalidOperationException>(() => p.AddRefund(Refund(30m, "K2")));
        // The rejected refund did not change state.
        Assert.Equal(80m, p.RefundedAmount);
    }

    [Fact]
    public void FullRefundMarksFullyRefunded()
    {
        var p = CapturedPayment(100m);
        p.AddRefund(Refund(100m, "K1"));
        Assert.True(p.IsFullyRefunded);
        Assert.Equal(0m, p.RemainingRefundable);
    }

    [Fact]
    public void FailedRefundDoesNotConsumeRefundableAmount()
    {
        var p = CapturedPayment(100m);
        p.AddRefund(Refund(100m, "K1", status: "FAILED"));
        Assert.Equal(0m, p.RefundedAmount);
        Assert.Equal(100m, p.RemainingRefundable);
    }

    [Fact]
    public void CannotRefundBeforeCapture()
    {
        var p = new OrderPayment("PPO1", "USD", 100m);
        Assert.Throws<InvalidOperationException>(() => p.AddRefund(Refund(10m, "K1")));
    }
}
