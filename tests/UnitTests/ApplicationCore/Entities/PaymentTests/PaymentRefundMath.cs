using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.PaymentTests;

public class PaymentRefundMath
{
    private static Payment AuthorizedPayment(decimal amount = 100m) =>
        new(orderId: 1, currency: "USD", amount: amount, payPalOrderId: "PPO-1",
            authorizationId: "AUTH-1", authorizationStatus: "CREATED", authorizationExpiresAt: DateTimeOffset.UtcNow.AddDays(29));

    [Fact]
    public void RemainingRefundableIsZeroBeforeCapture()
    {
        var payment = AuthorizedPayment();
        Assert.Equal(0m, payment.RemainingRefundable);
        Assert.Equal(0m, payment.TotalRefunded);
    }

    [Fact]
    public void RemainingRefundableEqualsCapturedBeforeAnyRefund()
    {
        var payment = AuthorizedPayment();
        payment.RecordCapture("CAP-1", "COMPLETED", 100m, payPalFee: 3.20m, netAmount: 96.80m);

        Assert.Equal(100m, payment.RemainingRefundable);
        Assert.Equal(96.80m, payment.NetAmount);
        Assert.Equal(3.20m, payment.PayPalFee);
    }

    [Fact]
    public void PartialRefundsAccumulateAndReduceRemaining()
    {
        var payment = AuthorizedPayment();
        payment.RecordCapture("CAP-1", "COMPLETED", 100m, 3m, 97m);

        payment.AddRefund("RF-1", 30m, "COMPLETED", "key-1");
        payment.AddRefund("RF-2", 20m, "COMPLETED", "key-2");

        Assert.Equal(50m, payment.TotalRefunded);
        Assert.Equal(50m, payment.RemainingRefundable);
        Assert.Equal(2, payment.Refunds.Count);
    }

    [Fact]
    public void FullRefundLeavesNothingRemaining()
    {
        var payment = AuthorizedPayment();
        payment.RecordCapture("CAP-1", "COMPLETED", 100m, 3m, 97m);
        payment.AddRefund("RF-1", 100m, "COMPLETED", "key-1");

        Assert.Equal(0m, payment.RemainingRefundable);
    }

    [Fact]
    public void ReauthorizationReplacesAuthorizationDetails()
    {
        var payment = AuthorizedPayment();
        var newExpiry = DateTimeOffset.UtcNow.AddDays(29);

        payment.RecordReauthorization("AUTH-2", "CREATED", newExpiry);

        Assert.Equal("AUTH-2", payment.AuthorizationId);
        Assert.Equal(newExpiry, payment.AuthorizationExpiresAt);
    }
}
