using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.InvestorTests;

public class InvestorRoundUpAndInvest
{
    private static Investor ActiveInvestor()
    {
        var investor = new Investor("shopper-1");
        investor.MarkActive();
        return investor;
    }

    [Theory]
    [InlineData(12.30, 0.70)]
    [InlineData(8.50, 0.50)]
    [InlineData(19.99, 0.01)]
    [InlineData(10.00, 0.00)] // already a whole number of euros
    [InlineData(0.00, 0.00)]
    public void RoundUpIsDifferenceToNextWholeEuro(double total, double expected)
    {
        Assert.Equal((decimal)expected, Investor.RoundUpFor((decimal)total));
    }

    [Fact]
    public void SetsNothingAsideWhenNotAnAcceptedInvestor()
    {
        var pending = new Investor("shopper-1"); // starts Pending
        Assert.Equal(0m, pending.SetAsideRoundUp(12.30m));
        Assert.Equal(0m, pending.PendingAmount);
    }

    [Fact]
    public void SetsAsideRoundUpForAcceptedInvestor()
    {
        var investor = ActiveInvestor();
        Assert.Equal(0.70m, investor.SetAsideRoundUp(12.30m));
        Assert.Equal(0.70m, investor.PendingAmount);
    }

    [Fact]
    public void DoesNotInvestBeforeReachingTheThreshold()
    {
        var investor = ActiveInvestor();
        for (var i = 0; i < 19; i++) investor.SetAsideRoundUp(8.50m); // 19 x 0.50 = 9.50
        Assert.Null(investor.StartInvestmentIfThresholdReached());
        Assert.Equal(9.50m, investor.PendingAmount);
    }

    [Fact]
    public void InvestsWholeBalanceOnceThresholdReachedAndResets()
    {
        var investor = ActiveInvestor();
        for (var i = 0; i < 20; i++) investor.SetAsideRoundUp(8.50m); // 20 x 0.50 = 10.00

        var investment = investor.StartInvestmentIfThresholdReached();

        Assert.NotNull(investment);
        Assert.Equal(10.00m, investment!.Amount);
        Assert.Equal(InvestmentStatus.Pending, investment.Status);
        Assert.Equal(0m, investor.PendingAmount);
        Assert.Equal(10.00m, investor.InvestedAmount);
    }

    [Fact]
    public void SettledInvestmentCountsAsInvested()
    {
        var investor = ActiveInvestor();
        for (var i = 0; i < 20; i++) investor.SetAsideRoundUp(8.50m);
        var investment = investor.StartInvestmentIfThresholdReached()!;

        investor.SettleInvestment(investment);

        Assert.Equal(InvestmentStatus.Settled, investment.Status);
        Assert.Equal(10.00m, investor.InvestedAmount);
        Assert.Equal(0m, investor.PendingAmount);
    }

    [Fact]
    public void FailedInvestmentReturnsMoneyToPendingAndIsNotCountedAsInvested()
    {
        var investor = ActiveInvestor();
        for (var i = 0; i < 20; i++) investor.SetAsideRoundUp(8.50m);
        var investment = investor.StartInvestmentIfThresholdReached()!;

        investor.FailInvestment(investment);

        Assert.Equal(InvestmentStatus.Failed, investment.Status);
        Assert.Equal(0m, investor.InvestedAmount);
        Assert.Equal(10.00m, investor.PendingAmount); // conserved — shopper does not lose the money
    }

    [Fact]
    public void RejectedInvestorCannotBecomeActive()
    {
        var investor = new Investor("shopper-1");
        investor.MarkRejected();
        investor.MarkActive();
        Assert.Equal(EnrolmentStatus.Rejected, investor.Status);
        Assert.False(investor.CanInvest);
    }
}
