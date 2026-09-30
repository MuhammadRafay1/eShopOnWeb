using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.UnitTests.ApplicationCore.Helpers;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.PaymentAggregateTests;

public class PaymentRefundTests
{
    private static Payment CreateCapturedPayment(decimal amount = 8.50m)
    {
        var payment = new Payment(orderId: 1, buyerId: "buyer@example.com", amount: amount, currencyCode: "USD", correlationReference: "ESHOP-1-abc");
        EntityIdHelper.SetId(payment, 1);
        payment.MarkAuthorized("AUTH-1", "CREATED", amount, null);
        payment.MarkCaptured("CAP-1", "COMPLETED", amount, 0.71m, amount - 0.71m);
        return payment;
    }

    [Fact]
    public void PartialRefundMovesToPartiallyRefunded()
    {
        var payment = CreateCapturedPayment();

        payment.AddRefund("REF-1", 1.00m, "COMPLETED", "key-1");

        Assert.Equal(PaymentStatus.PartiallyRefunded, payment.Status);
        Assert.Equal(1.00m, payment.TotalRefunded());
    }

    [Fact]
    public void TwoDistinctPartialRefundsAreBothAllowedAndAccumulate()
    {
        var payment = CreateCapturedPayment();

        payment.AddRefund("REF-1", 1.00m, "COMPLETED", "key-1");
        payment.AddRefund("REF-2", 2.00m, "COMPLETED", "key-2");

        Assert.Equal(3.00m, payment.TotalRefunded());
        Assert.Equal(2, payment.Refunds.Count);
    }

    [Fact]
    public void RefundingTheFullCapturedAmountMovesToRefunded()
    {
        var payment = CreateCapturedPayment(8.50m);

        payment.AddRefund("REF-1", 8.50m, "COMPLETED", "key-1");

        Assert.Equal(PaymentStatus.Refunded, payment.Status);
    }

    [Fact]
    public void OverRefundIsRejected()
    {
        var payment = CreateCapturedPayment(8.50m);
        payment.AddRefund("REF-1", 5.00m, "COMPLETED", "key-1");

        // Already refunded 5.00 of 8.50 captured; requesting 5.00 more would exceed it.
        Assert.Throws<PaymentRejectedException>(() => payment.AddRefund("REF-2", 5.00m, "COMPLETED", "key-2"));

        // The rejected attempt must not have been recorded.
        Assert.Equal(5.00m, payment.TotalRefunded());
        Assert.Single(payment.Refunds);
    }

    [Fact]
    public void RefundingBeforeCaptureIsRejected()
    {
        var payment = new Payment(orderId: 1, buyerId: "buyer@example.com", amount: 8.50m, currencyCode: "USD", correlationReference: "ESHOP-1-abc");
        payment.MarkAuthorized("AUTH-1", "CREATED", 8.50m, null);

        Assert.Throws<PaymentStateConflictException>(() => payment.AddRefund("REF-1", 1.00m, "COMPLETED", "key-1"));
    }

    [Fact]
    public void FindRefundByKeyReturnsTheExistingRefundForARepeatedKey()
    {
        var payment = CreateCapturedPayment();
        payment.AddRefund("REF-1", 1.00m, "COMPLETED", "key-1");

        var found = payment.FindRefundByKey("key-1");

        Assert.NotNull(found);
        Assert.Equal("REF-1", found!.RefundId);
    }

    [Fact]
    public void FindRefundByKeyReturnsNullForAnUnknownKey()
    {
        var payment = CreateCapturedPayment();

        Assert.Null(payment.FindRefundByKey("never-used"));
    }
}
