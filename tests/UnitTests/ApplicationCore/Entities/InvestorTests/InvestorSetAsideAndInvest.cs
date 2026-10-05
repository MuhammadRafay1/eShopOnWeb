using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.InvestorTests;

public class InvestorSetAsideAndInvest
{
    private static Investor AcceptedInvestor()
    {
        var investor = new Investor("buyer@example.com");
        investor.MarkActive();
        return investor;
    }

    [Fact]
    public void SetsNothingAsideWhenNotAccepted()
    {
        var investor = new Investor("buyer@example.com"); // Pending

        var roundUp = investor.SetAsideRoundUp(1230);

        Assert.Equal(0, roundUp);
        Assert.Equal(0, investor.SetAsideCents);
    }

    [Fact]
    public void SetsAsideRoundUpToNextEuro()
    {
        var investor = AcceptedInvestor();

        var roundUp = investor.SetAsideRoundUp(1230); // €12.30 -> €0.70

        Assert.Equal(70, roundUp);
        Assert.Equal(70, investor.SetAsideCents);
    }

    [Fact]
    public void SetsNothingAsideForWholeEuroTotal()
    {
        var investor = AcceptedInvestor();

        var roundUp = investor.SetAsideRoundUp(1200); // €12.00

        Assert.Equal(0, roundUp);
        Assert.Equal(0, investor.SetAsideCents);
    }

    [Fact]
    public void DoesNotBeginInvestmentBelowThreshold()
    {
        var investor = AcceptedInvestor();
        investor.SetAsideRoundUp(1050); // sets aside 50c

        var investment = investor.TryBeginInvestment();

        Assert.Null(investment);
        Assert.Equal(50, investor.SetAsideCents);
        Assert.Empty(investor.Investments);
    }

    [Fact]
    public void BeginsInvestmentForWholeBalanceAndResetsOnceThresholdReached()
    {
        var investor = AcceptedInvestor();
        for (var i = 0; i < 20; i++)
            investor.SetAsideRoundUp(850); // 50c each -> 1000c after 20

        var investment = investor.TryBeginInvestment();

        Assert.NotNull(investment);
        Assert.Equal(1000, investment!.AmountCents);
        Assert.Equal(InvestmentStatus.Pending, investment.Status);
        Assert.Equal(0, investor.SetAsideCents);
        Assert.Single(investor.Investments);
    }

    [Fact]
    public void InvestedCentsCountsOnlySettledInvestments()
    {
        var investor = AcceptedInvestor();
        for (var i = 0; i < 20; i++) investor.SetAsideRoundUp(850);
        var investment = investor.TryBeginInvestment()!;

        Assert.Equal(0, investor.InvestedCents); // still pending

        investment.MarkSettled();
        Assert.Equal(1000, investor.InvestedCents);
    }

    [Fact]
    public void FailedInvestmentIsRefundedToBalance()
    {
        var investor = AcceptedInvestor();
        for (var i = 0; i < 20; i++) investor.SetAsideRoundUp(850);
        var investment = investor.TryBeginInvestment()!;

        investment.MarkFailed();
        investor.RefundFailedInvestment(investment);

        Assert.Equal(1000, investor.SetAsideCents);
        Assert.Equal(0, investor.InvestedCents);
    }
}
