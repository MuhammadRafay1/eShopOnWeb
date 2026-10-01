using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.PaymentTests;

public class OrderPaymentStateMachineTests
{
    private static OrderPayment NewPayment() => new(orderId: 1, invoiceId: "ord-1", amount: 10.00m, currencyCode: "USD");

    [Fact]
    public void NewPayment_StartsAuthorizing()
    {
        var payment = NewPayment();
        Assert.Equal(PaymentStatus.Authorizing, payment.Status);
    }

    [Fact]
    public void AuthorizationSucceeded_MovesToAuthorized()
    {
        var payment = NewPayment();
        payment.AuthorizationSucceeded("pp-order-1", "auth-1", "CREATED", null, "card ending 1111");

        Assert.Equal(PaymentStatus.Authorized, payment.Status);
        Assert.Equal("auth-1", payment.AuthorizationId);
    }

    [Fact]
    public void AuthorizationFailed_AllowsRetryViaBeginAuthorizing()
    {
        var payment = NewPayment();
        payment.AuthorizationFailed();
        Assert.Equal(PaymentStatus.AuthorizationFailed, payment.Status);

        payment.BeginAuthorizing();
        Assert.Equal(PaymentStatus.Authorizing, payment.Status);
    }

    [Fact]
    public void Fulfil_ThenCapture_MovesToCaptured()
    {
        var payment = NewPayment();
        payment.AuthorizationSucceeded("pp-order-1", "auth-1", "CREATED", null, "card ending 1111");
        payment.BeginFulfilling();
        payment.CaptureSucceeded("cap-1", "COMPLETED", 10.00m, 0.59m, 9.41m);

        Assert.Equal(PaymentStatus.Captured, payment.Status);
        Assert.Equal(10.00m, payment.CapturedGross);
        Assert.Equal(0m, payment.RefundedTotal);
        Assert.Equal(10.00m, payment.RemainingRefundable);
    }

    [Fact]
    public void Cancel_FromAuthorized_Succeeds()
    {
        var payment = NewPayment();
        payment.AuthorizationSucceeded("pp-order-1", "auth-1", "CREATED", null, null);
        payment.Cancel();

        Assert.Equal(PaymentStatus.Cancelled, payment.Status);
    }

    [Fact]
    public void Cancel_AfterCapture_Throws()
    {
        var payment = NewPayment();
        payment.AuthorizationSucceeded("pp-order-1", "auth-1", "CREATED", null, null);
        payment.BeginFulfilling();
        payment.CaptureSucceeded("cap-1", "COMPLETED", 10.00m, 0.59m, 9.41m);

        Assert.Throws<PaymentAuthorizationException>(() => payment.Cancel());
    }

    [Fact]
    public void Fulfil_WithoutAuthorization_Throws()
    {
        var payment = NewPayment();
        Assert.Throws<PaymentAuthorizationException>(() => payment.BeginFulfilling());
    }

    [Fact]
    public void PartialRefund_StaysPartiallyRefunded_UntilFullyRefunded()
    {
        var payment = NewPayment();
        payment.AuthorizationSucceeded("pp-order-1", "auth-1", "CREATED", null, null);
        payment.BeginFulfilling();
        payment.CaptureSucceeded("cap-1", "COMPLETED", 10.00m, 0.59m, 9.41m);

        payment.RecordRefund(4.00m);
        Assert.Equal(PaymentStatus.PartiallyRefunded, payment.Status);
        Assert.Equal(6.00m, payment.RemainingRefundable);

        payment.RecordRefund(6.00m);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(0m, payment.RemainingRefundable);
    }

    [Fact]
    public void Refund_BeyondCapturedAmount_Throws()
    {
        var payment = NewPayment();
        payment.AuthorizationSucceeded("pp-order-1", "auth-1", "CREATED", null, null);
        payment.BeginFulfilling();
        payment.CaptureSucceeded("cap-1", "COMPLETED", 10.00m, 0.59m, 9.41m);

        Assert.Throws<RefundAmountExceededException>(() => payment.RecordRefund(10.01m));
    }

    [Fact]
    public void Refund_AfterPartialThenExceedingRemainder_Throws()
    {
        var payment = NewPayment();
        payment.AuthorizationSucceeded("pp-order-1", "auth-1", "CREATED", null, null);
        payment.BeginFulfilling();
        payment.CaptureSucceeded("cap-1", "COMPLETED", 10.00m, 0.59m, 9.41m);
        payment.RecordRefund(7.00m);

        Assert.Throws<RefundAmountExceededException>(() => payment.RecordRefund(3.01m));
    }
}
