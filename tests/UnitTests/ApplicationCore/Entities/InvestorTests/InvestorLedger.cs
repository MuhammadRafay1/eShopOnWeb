using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.InvestorTests;

public class InvestorLedger
{
    private static Investor NewInvestor()
    {
        var investor = new Investor("shopper@example.com");
        investor.RecordUpvestUser(System.Guid.NewGuid(), null);
        investor.RecordUpvestAccount(System.Guid.NewGuid(), System.Guid.NewGuid());
        investor.MarkActive();
        return investor;
    }

    [Theory]
    [InlineData(12.30, 0.70)]
    [InlineData(0.01, 0.99)]
    [InlineData(99.99, 0.01)]
    public void SetsAsideDifferenceToNextWholeEuro(double total, double expectedRoundUp)
    {
        var investor = NewInvestor();

        var roundUp = investor.SetAsideRoundUp((decimal)total);

        Assert.Equal((decimal)expectedRoundUp, roundUp);
        Assert.Equal((decimal)expectedRoundUp, investor.SetAsideAmount);
    }

    [Theory]
    [InlineData(10.00)]
    [InlineData(1.00)]
    public void WholeEuroTotalSetsAsideNothing(double total)
    {
        var investor = NewInvestor();

        var roundUp = investor.SetAsideRoundUp((decimal)total);

        Assert.Equal(0m, roundUp);
        Assert.Equal(0m, investor.SetAsideAmount);
    }

    [Fact]
    public void RoundUpsAccumulate()
    {
        var investor = NewInvestor();

        investor.SetAsideRoundUp(12.30m); // 0.70
        investor.SetAsideRoundUp(8.50m);  // 0.50

        Assert.Equal(1.20m, investor.SetAsideAmount);
    }

    [Fact]
    public void IsReadyToInvestOnlyAtThreshold()
    {
        var investor = NewInvestor();

        investor.SetAsideRoundUp(0.50m); // not whole: wait, 0.50 -> round up 0.50
        Assert.False(investor.IsReadyToInvest);

        for (var i = 0; i < 19; i++)
        {
            investor.SetAsideRoundUp(0.50m);
        }

        Assert.True(investor.IsReadyToInvest);
        Assert.True(investor.SetAsideAmount >= Investor.InvestmentThreshold);
    }

    [Fact]
    public void BeginInvestmentMovesWholeBalanceAndResets()
    {
        var investor = NewInvestor();
        for (var i = 0; i < 21; i++)
        {
            investor.SetAsideRoundUp(0.50m); // 21 * 0.50 = 10.50
        }

        var balanceBefore = investor.SetAsideAmount;
        var investment = investor.BeginInvestment();

        Assert.Equal(balanceBefore, investment.Amount);
        Assert.Equal(InvestmentStatus.Pending, investment.Status);
        Assert.Equal(0m, investor.SetAsideAmount);
        Assert.Equal(balanceBefore, investor.InvestedAmount); // pending counts as invested
        Assert.Single(investor.Investments);
    }

    [Fact]
    public void FailedInvestmentReturnsMoneyToBalanceAndIsExcludedFromInvested()
    {
        var investor = NewInvestor();
        for (var i = 0; i < 21; i++)
        {
            investor.SetAsideRoundUp(0.50m);
        }

        var investment = investor.BeginInvestment();
        investor.FailInvestment(investment);

        Assert.Equal(InvestmentStatus.Failed, investment.Status);
        Assert.Equal(10.50m, investor.SetAsideAmount); // money back to pending
        Assert.Equal(0m, investor.InvestedAmount);     // failed excluded
    }

    [Fact]
    public void SettledInvestmentCountsTowardInvested()
    {
        var investor = NewInvestor();
        for (var i = 0; i < 20; i++)
        {
            investor.SetAsideRoundUp(0.50m);
        }

        var investment = investor.BeginInvestment();
        investment.MarkSettled();

        Assert.Equal(InvestmentStatus.Settled, investor.Investments.Single().Status);
        Assert.Equal(10.00m, investor.InvestedAmount);
        Assert.Equal(0m, investor.SetAsideAmount);
    }
}
