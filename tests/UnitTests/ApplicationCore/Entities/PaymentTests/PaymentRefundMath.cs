using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.PaymentTests;

public class PaymentRefundMath
{
    private static Payment CapturedPayment(decimal amount = 100m)
    {
        var p = new Payment(orderId: 1, amount: amount, currencyCode: "USD");
        p.SetAuthorization("PPORDER", "AUTH1", "CREATED", null);
        p.SetCapture("CAP1", "COMPLETED", amount, paypalFee: 3m, netAmount: amount - 3m);
        return p;
    }

    [Fact]
    public void NewPaymentGeneratesIdempotencyKey()
    {
        var p = new Payment(1, 10m, "USD");
        Assert.NotEqual(default, p.IdempotencyKey);
        Assert.False(p.IsAuthorized);
    }

    [Fact]
    public void TotalRefundedSumsCompletedAndPendingButNotFailed()
    {
        var p = CapturedPayment();
        p.AddRefund("R1", 20m, "COMPLETED", "k1");
        p.AddRefund("R2", 10m, "PENDING", "k2");
        p.AddRefund("R3", 50m, "FAILED", "k3");
        p.AddRefund("R4", 50m, "CANCELLED", "k4");

        Assert.Equal(30m, p.TotalRefunded());
        Assert.Equal(70m, p.RefundableRemaining());
    }

    [Fact]
    public void FindRefundByIdempotencyKeyReturnsExisting()
    {
        var p = CapturedPayment();
        p.AddRefund("R1", 20m, "COMPLETED", "dup-key");

        Assert.NotNull(p.FindRefundByIdempotencyKey("dup-key"));
        Assert.Null(p.FindRefundByIdempotencyKey("other-key"));
    }

    [Fact]
    public void FullyRefundedLeavesNoRemainder()
    {
        var p = CapturedPayment(50m);
        p.AddRefund("R1", 50m, "COMPLETED", "k1");
        Assert.Equal(0m, p.RefundableRemaining());
    }
}
