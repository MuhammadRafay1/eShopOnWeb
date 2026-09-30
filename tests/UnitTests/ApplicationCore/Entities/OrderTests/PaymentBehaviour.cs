using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.OrderTests;

public class PaymentBehaviour
{
    private static Payment Captured()
    {
        var payment = new Payment("USD", "PPORDER1", "AUTH1", "CREATED", 29m,
            DateTimeOffset.UtcNow.AddDays(29), null);
        payment.RecordCapture("CAP1", "COMPLETED", 29m, 1.24m, 27.76m, DateTimeOffset.UtcNow);
        return payment;
    }

    [Fact]
    public void RecordCaptureStoresPayPalReportedFigures()
    {
        var payment = Captured();
        Assert.Equal("CAP1", payment.CaptureId);
        Assert.Equal(29m, payment.CapturedAmount);
        Assert.Equal(1.24m, payment.PayPalFee);
        Assert.Equal(27.76m, payment.NetAmount);
        Assert.Equal("CAPTURED", payment.AuthorizationStatus);
    }

    [Fact]
    public void CannotRefundBeforeCapture()
    {
        var payment = new Payment("USD", "PPORDER1", "AUTH1", "CREATED", 29m, null, null);
        Assert.Throws<OrderStateException>(() => payment.AddRefund("R1", 5m, "COMPLETED", "key-1"));
    }

    [Fact]
    public void PartialRefundsAccumulateAndCapAtCapturedAmount()
    {
        var payment = Captured();

        payment.AddRefund("R1", 10m, "COMPLETED", "key-1");
        Assert.Equal(10m, payment.RefundedAmount);
        Assert.Equal(19m, payment.RemainingRefundable);
        Assert.Equal("PARTIALLY_REFUNDED", payment.CaptureStatus);

        payment.AddRefund("R2", 19m, "COMPLETED", "key-2");
        Assert.Equal(29m, payment.RefundedAmount);
        Assert.Equal("REFUNDED", payment.CaptureStatus);
    }

    [Fact]
    public void RefundBeyondCapturedAmountThrows()
    {
        var payment = Captured();
        payment.AddRefund("R1", 20m, "COMPLETED", "key-1");
        Assert.Throws<RefundExceedsCaptureException>(
            () => payment.AddRefund("R2", 10m, "COMPLETED", "key-2"));
    }

    [Fact]
    public void FindsRefundByIdempotencyKey()
    {
        var payment = Captured();
        var refund = payment.AddRefund("R1", 10m, "COMPLETED", "key-1");
        Assert.Same(refund, payment.FindRefundByIdempotencyKey("key-1"));
        Assert.Null(payment.FindRefundByIdempotencyKey("other"));
    }

    [Fact]
    public void ReauthorizationReplacesAuthorizationState()
    {
        var payment = new Payment("USD", "PPORDER1", "AUTH1", "CREATED", 29m,
            DateTimeOffset.UtcNow.AddDays(-1), null);
        payment.RecordReauthorization("AUTH2", "CREATED", 29m, DateTimeOffset.UtcNow.AddDays(29));
        Assert.Equal("AUTH2", payment.AuthorizationId);
        Assert.True(payment.IsAuthorizationCapturable);
        Assert.False(payment.IsAuthorizationExpired);
    }

    [Fact]
    public void VoidMarksAuthorizationVoided()
    {
        var payment = new Payment("USD", "PPORDER1", "AUTH1", "CREATED", 29m, null, null);
        payment.MarkVoided();
        Assert.Equal("VOIDED", payment.AuthorizationStatus);
    }
}
