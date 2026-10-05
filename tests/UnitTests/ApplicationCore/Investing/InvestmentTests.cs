using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Investing;

public class InvestmentTests
{
    [Fact]
    public void New_investment_is_pending_unfunded_and_has_stable_keys()
    {
        var i = new Investment("buyer-1", 1050);
        Assert.Equal(InvestmentStatus.Pending, i.Status);
        Assert.False(i.Funded);
        Assert.Null(i.UpvestOrderId);
        Assert.NotEqual(default, i.OrderIdempotencyKey);
        Assert.False(string.IsNullOrEmpty(i.ClientReference));
    }

    [Fact]
    public void Lifecycle_fund_place_settle()
    {
        var i = new Investment("buyer-1", 1050);
        i.MarkFunded();
        i.RecordOrder("order-1");
        i.Settle();
        Assert.True(i.Funded);
        Assert.Equal("order-1", i.UpvestOrderId);
        Assert.Equal(InvestmentStatus.Settled, i.Status);
    }

    [Fact]
    public void Can_fail()
    {
        var i = new Investment("buyer-1", 1050);
        i.Fail();
        Assert.Equal(InvestmentStatus.Failed, i.Status);
    }
}
