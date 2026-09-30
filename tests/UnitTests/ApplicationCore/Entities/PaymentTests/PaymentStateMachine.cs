using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.PaymentTests;

public class PaymentStateMachine
{
    private static Payment NewAuthorizedPayment(decimal amount = 100m)
    {
        var payment = new Payment(orderId: 1, buyerId: "buyer@example.com", amount: amount, currencyCode: "USD", invoiceReference: "ESHOP-test-1");
        payment.MarkAuthorized("PPORDER1", "AUTH1", "CREATED", null, "VISA", "1111");
        return payment;
    }

    private static Payment NewCapturedPayment(decimal amount = 100m)
    {
        var payment = NewAuthorizedPayment(amount);
        payment.MarkCaptured("CAP1", amount, 3m, amount - 3m);
        return payment;
    }

    [Fact]
    public void StartsInAwaitingPayment()
    {
        var payment = new Payment(1, "buyer@example.com", 10m, "USD", "ESHOP-test-1");
        Assert.Equal(PaymentStatus.AwaitingPayment, payment.Status);
    }

    [Fact]
    public void MarkAuthorizedMovesToAuthorized()
    {
        var payment = NewAuthorizedPayment();
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
        Assert.Equal("AUTH1", payment.AuthorizationId);
    }

    [Fact]
    public void MarkCapturedRequiresAuthorizedFirst()
    {
        var payment = new Payment(1, "buyer@example.com", 10m, "USD", "ESHOP-test-1");
        Assert.Throws<PaymentConflictException>(() => payment.MarkCaptured("CAP1", 10m, 1m, 9m));
    }

    [Fact]
    public void MarkCapturedMovesToCaptured()
    {
        var payment = NewCapturedPayment(100m);
        Assert.Equal(PaymentStatus.Captured, payment.Status);
        Assert.Equal(100m, payment.CapturedAmount);
    }

    [Fact]
    public void MarkCancelledRequiresAuthorized()
    {
        var payment = new Payment(1, "buyer@example.com", 10m, "USD", "ESHOP-test-1");
        Assert.Throws<PaymentConflictException>(() => payment.MarkCancelled());
    }

    [Fact]
    public void MarkCancelledAfterCaptureIsRejected()
    {
        var payment = NewCapturedPayment();
        Assert.Throws<PaymentConflictException>(() => payment.MarkCancelled());
    }

    [Fact]
    public void PartialRefundMovesToPartiallyRefunded()
    {
        var payment = NewCapturedPayment(100m);
        payment.AddRefund(new Refund("R1", 40m, "COMPLETED", "key1"));

        Assert.Equal(PaymentStatus.PartiallyRefunded, payment.Status);
        Assert.Equal(40m, payment.TotalRefunded());
    }

    [Fact]
    public void FullRefundMovesToRefunded()
    {
        var payment = NewCapturedPayment(100m);
        payment.AddRefund(new Refund("R1", 100m, "COMPLETED", "key1"));

        Assert.Equal(PaymentStatus.Refunded, payment.Status);
    }

    [Fact]
    public void TwoPartialRefundsThatSumToCapturedAmountFullyRefund()
    {
        var payment = NewCapturedPayment(100m);
        payment.AddRefund(new Refund("R1", 40m, "COMPLETED", "key1"));
        payment.AddRefund(new Refund("R2", 60m, "COMPLETED", "key2"));

        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(100m, payment.TotalRefunded());
    }

    [Fact]
    public void RefundBeyondCapturedAmountIsRejected()
    {
        var payment = NewCapturedPayment(100m);
        payment.AddRefund(new Refund("R1", 60m, "COMPLETED", "key1"));

        Assert.Throws<PaymentConflictException>(() => payment.AddRefund(new Refund("R2", 60m, "COMPLETED", "key2")));
        // The first refund must still stand - a rejected second refund must not roll back the first.
        Assert.Equal(60m, payment.TotalRefunded());
        Assert.Equal(PaymentStatus.PartiallyRefunded, payment.Status);
    }

    [Fact]
    public void RefundRequiresCapturedFirst()
    {
        var payment = NewAuthorizedPayment();
        Assert.Throws<PaymentConflictException>(() => payment.AddRefund(new Refund("R1", 10m, "COMPLETED", "key1")));
    }
}
