using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.PortfolioAggregate;

public class PaymentAndRealignmentTests
{
    [Fact]
    public void New_payment_is_pending_and_scoped_to_its_buyer()
    {
        var id = Guid.NewGuid();
        var payment = new Payment(id, "buyer-1", 100.00m, Guid.NewGuid());

        Assert.Equal(id, payment.PaymentId);
        Assert.Equal("buyer-1", payment.BuyerId);
        Assert.Equal(100.00m, payment.Amount);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Null(payment.UpvestOrderId);
    }

    [Fact]
    public void Payment_rejects_non_positive_amounts()
    {
        Assert.ThrowsAny<ArgumentException>(() => new Payment(Guid.NewGuid(), "buyer-1", 0m, Guid.NewGuid()));
        Assert.ThrowsAny<ArgumentException>(() => new Payment(Guid.NewGuid(), "buyer-1", -5m, Guid.NewGuid()));
    }

    [Fact]
    public void Payment_records_order_and_status()
    {
        var payment = new Payment(Guid.NewGuid(), "buyer-1", 50m, Guid.NewGuid());
        var orderId = Guid.NewGuid();

        payment.RecordOrder(orderId);
        payment.SetStatus(PaymentStatus.Invested);

        Assert.Equal(orderId, payment.UpvestOrderId);
        Assert.Equal(PaymentStatus.Invested, payment.Status);
    }

    [Fact]
    public void New_realignment_is_pending()
    {
        var id = Guid.NewGuid();
        var realignment = new Realignment(id, "balanced", Guid.NewGuid());

        Assert.Equal(id, realignment.RealignmentId);
        Assert.Equal("balanced", realignment.ModelId);
        Assert.Equal(RealignmentStatus.Pending, realignment.Status);
        Assert.Null(realignment.UpvestExecutionId);
    }

    [Fact]
    public void Realignment_records_execution_and_status()
    {
        var realignment = new Realignment(Guid.NewGuid(), "balanced", Guid.NewGuid());
        var executionId = Guid.NewGuid();

        realignment.RecordExecution(executionId);
        realignment.SetStatus(RealignmentStatus.Completed);

        Assert.Equal(executionId, realignment.UpvestExecutionId);
        Assert.Equal(RealignmentStatus.Completed, realignment.Status);
    }
}
