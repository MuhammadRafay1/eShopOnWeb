using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.InvestingTests;

public class InvestorTests
{
    private static Investor ActiveInvestor()
    {
        var investor = new Investor("buyer-1");
        investor.LinkUpvestUser("upvest-user", "upvest-account");
        investor.MarkActive();
        return investor;
    }

    [Fact]
    public void NewInvestorStartsPendingWithNoBalance()
    {
        var investor = new Investor("buyer-1");

        Assert.Equal(InvestorStatus.Pending, investor.Status);
        Assert.False(investor.IsActive);
        Assert.Equal(0m, investor.PendingAmount);
        Assert.Equal(0m, investor.InvestedAmount);
    }

    [Fact]
    public void SetsNothingAsideWhenNotActive()
    {
        var investor = new Investor("buyer-1");

        var setAside = investor.SetAside(orderId: 1, roundUp: 0.70m);

        Assert.Equal(0m, setAside);
        Assert.Equal(0m, investor.PendingAmount);
        Assert.Empty(investor.Ledger);
    }

    [Fact]
    public void AccruesRoundUpWhenActive()
    {
        var investor = ActiveInvestor();

        var setAside = investor.SetAside(orderId: 1, roundUp: 0.70m);

        Assert.Equal(0.70m, setAside);
        Assert.Equal(0.70m, investor.PendingAmount);
        Assert.Single(investor.Ledger);
    }

    [Fact]
    public void AZeroRoundUpIsANoOp()
    {
        var investor = ActiveInvestor();

        var setAside = investor.SetAside(orderId: 1, roundUp: 0m);

        Assert.Equal(0m, setAside);
        Assert.Empty(investor.Ledger);
    }

    [Fact]
    public void IsReadyToInvestOnlyAtThreshold()
    {
        var investor = ActiveInvestor();

        investor.SetAside(1, 9.90m);
        Assert.False(investor.IsReadyToInvest);

        investor.SetAside(2, 0.10m); // now 10.00
        Assert.True(investor.IsReadyToInvest);
    }

    [Fact]
    public void BeginInvestmentInvestsWholeBalanceAndResetsToZero()
    {
        var investor = ActiveInvestor();
        investor.SetAside(1, 10.40m);

        var investment = investor.BeginInvestment("IE00B4L5Y983", "upvest-order-1");

        Assert.Equal(10.40m, investment.Amount);
        Assert.Equal(InvestmentStatus.Pending, investment.Status);
        Assert.Equal(0m, investor.PendingAmount);          // starts again from zero
        Assert.Equal(10.40m, investor.InvestedAmount);
        Assert.Single(investor.Investments);
    }

    [Fact]
    public void FailedInvestmentsAreExcludedFromInvestedAmount()
    {
        var investor = ActiveInvestor();
        investor.SetAside(1, 10.00m);
        var investment = investor.BeginInvestment("IE00B4L5Y983", "upvest-order-1");

        investment.MarkFailed();

        Assert.Equal(0m, investor.InvestedAmount);
    }

    [Fact]
    public void SettledInvestmentsCountTowardsInvestedAmount()
    {
        var investor = ActiveInvestor();
        investor.SetAside(1, 12.00m);
        var investment = investor.BeginInvestment("IE00B4L5Y983", "upvest-order-1");

        investment.MarkSettled();

        Assert.Equal(12.00m, investor.InvestedAmount);
    }
}
