using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Portfolio;

public class PortfolioStatusTests
{
    [Theory]
    [InlineData("ACTIVE", "ACTIVE", "active")]
    [InlineData("ACTIVE", "PENDING_APPROVAL", "pending")]
    [InlineData("INACTIVE", null, "pending")]
    [InlineData("INACTIVE", "ACTIVE", "pending")]
    [InlineData("OFFBOARDING", "ACTIVE", "rejected")]
    [InlineData("OFFBOARDED", null, "rejected")]
    public void MapsInvestorStatus(string userStatus, string? accountStatus, string expected)
    {
        Assert.Equal(expected, PortfolioStatus.FromUpvest(userStatus, accountStatus));
    }

    [Theory]
    [InlineData("NEW", "pending")]
    [InlineData("PROCESSING", "pending")]
    [InlineData("FILLED", "invested")]
    [InlineData("SETTLED", "invested")]
    [InlineData("CANCELLED", "failed")]
    [InlineData("", "pending")]
    public void MapsPaymentStatus(string orderStatus, string expected)
    {
        Assert.Equal(expected, PortfolioStatus.FromPortfolioOrder(orderStatus));
    }

    [Fact]
    public void RealignmentPendingWhenNoOrders()
    {
        Assert.Equal("pending", PortfolioStatus.FromRebalancingOrders(new string[0]));
    }

    [Fact]
    public void RealignmentCompletedWhenAllTerminalSuccess()
    {
        Assert.Equal("completed", PortfolioStatus.FromRebalancingOrders(new[] { "FILLED", "SETTLED" }));
    }

    [Fact]
    public void RealignmentPendingWhileAnyInFlight()
    {
        Assert.Equal("pending", PortfolioStatus.FromRebalancingOrders(new[] { "FILLED", "PROCESSING" }));
    }

    [Fact]
    public void RealignmentFailedWhenCancelledAndNonePending()
    {
        Assert.Equal("failed", PortfolioStatus.FromRebalancingOrders(new[] { "FILLED", "CANCELLED" }));
    }
}
