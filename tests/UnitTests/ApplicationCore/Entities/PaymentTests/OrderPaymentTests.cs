using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.PaymentTests;

public class OrderPaymentTests
{
    private static OrderPayment Captured()
    {
        var p = new OrderPayment(1, "buyer", "USD", 30m, "INV-1");
        p.MarkAuthorized("PPO", "AUTH", "CREATED", DateTimeOffset.UtcNow.AddDays(28));
        p.MarkCaptured("CAP", "COMPLETED", 30m, 1m, 29m);
        return p;
    }

    [Fact]
    public void New_payment_awaits_payment_with_distinct_reference()
    {
        var p = new OrderPayment(1, "buyer", "USD", 10m, "INV");
        Assert.Equal(PaymentStatus.AwaitingPayment, p.Status);
        Assert.False(string.IsNullOrEmpty(p.PaymentReference));
        Assert.Equal(p.PaymentReference, p.CreateOrderIdempotencyKey);
        Assert.NotEqual(p.CreateOrderIdempotencyKey, p.CaptureIdempotencyKey);
    }

    [Fact]
    public void Refundable_remaining_tracks_counted_refunds()
    {
        var p = Captured();
        p.AddRefundClaim("k1", 10m).Settle("R1", "COMPLETED");
        p.RecalculateRefundState();

        Assert.Equal(10m, p.TotalRefunded());
        Assert.Equal(20m, p.RefundableRemaining());
        Assert.Equal(PaymentStatus.PartiallyRefunded, p.Status);
    }

    [Fact]
    public void Failed_refund_does_not_count_against_captured()
    {
        var p = Captured();
        var failed = p.AddRefundClaim("k1", 10m);
        failed.MarkFailed();
        p.RecalculateRefundState();

        Assert.Equal(0m, p.TotalRefunded());
        Assert.Equal(30m, p.RefundableRemaining());
        Assert.Equal(PaymentStatus.Captured, p.Status);
    }

    [Fact]
    public void Full_refund_marks_refunded()
    {
        var p = Captured();
        p.AddRefundClaim("k1", 30m).Settle("R1", "COMPLETED");
        p.RecalculateRefundState();

        Assert.Equal(PaymentStatus.Refunded, p.Status);
        Assert.Equal(0m, p.RefundableRemaining());
    }

    [Fact]
    public void MarkDeclined_rotates_reference()
    {
        var p = new OrderPayment(1, "buyer", "USD", 10m, "INV");
        var before = p.PaymentReference;
        p.MarkDeclined("declined");
        Assert.Equal(PaymentStatus.Failed, p.Status);
        Assert.NotEqual(before, p.PaymentReference);
    }
}
