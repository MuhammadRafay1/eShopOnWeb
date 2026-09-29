using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.PaymentTests;

public class PaymentRefundGuard
{
    private static Payment CapturedPayment(decimal amount = 100m)
    {
        var payment = new Payment(orderId: 1, currency: "USD", amount: amount,
            payPalOrderId: "PPORDER", payPalAuthorizationId: "PPAUTH",
            authorizationStatus: "CREATED", authorizationExpiresAt: DateTimeOffset.UtcNow.AddDays(3));
        payment.Capture("PPCAPTURE", "COMPLETED", amount, fee: 3m, net: amount - 3m);
        return payment;
    }

    [Fact]
    public void PartialRefundLeavesPaymentPartiallyRefunded()
    {
        var payment = CapturedPayment(100m);
        payment.RecordRefund(new Refund("R1", "key-1", 40m, "COMPLETED"));

        Assert.Equal(PaymentStatus.PartiallyRefunded, payment.Status);
        Assert.Equal(40m, payment.RefundedAmount);
        Assert.Equal(60m, payment.RefundableRemaining);
    }

    [Fact]
    public void RefundsUpToCapturedAmountMarkPaymentRefunded()
    {
        var payment = CapturedPayment(100m);
        payment.RecordRefund(new Refund("R1", "key-1", 60m, "COMPLETED"));
        payment.RecordRefund(new Refund("R2", "key-2", 40m, "COMPLETED"));

        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(100m, payment.RefundedAmount);
    }

    [Fact]
    public void CannotRefundBeyondCapturedAmount()
    {
        var payment = CapturedPayment(100m);
        payment.RecordRefund(new Refund("R1", "key-1", 80m, "COMPLETED"));

        Assert.ThrowsAny<Exception>(() =>
            payment.RecordRefund(new Refund("R2", "key-2", 40m, "COMPLETED")));
        Assert.Equal(80m, payment.RefundedAmount);
    }

    [Fact]
    public void CannotRefundAnUncapturedPayment()
    {
        var payment = new Payment(1, "USD", 100m, "PPORDER", "PPAUTH", "CREATED",
            DateTimeOffset.UtcNow.AddDays(3));
        Assert.ThrowsAny<Exception>(() =>
            payment.RecordRefund(new Refund("R1", "key-1", 10m, "COMPLETED")));
    }
}
