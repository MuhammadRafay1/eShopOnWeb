using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.InvestorTests;

public class InvestorBehaviour
{
    private static Investor ActiveInvestor()
    {
        var investor = new Investor("buyer-1");
        investor.LinkUpvestUser("u");
        investor.LinkAccountGroup("g");
        investor.LinkAccount("a");
        investor.Activate();
        return investor;
    }

    [Fact]
    public void NewInvestorStartsPendingWithNothingSetAside()
    {
        var investor = new Investor("buyer-1");
        Assert.Equal(EnrolmentStatus.Pending, investor.Status);
        Assert.Equal(0m, investor.PendingAmount);
    }

    [Fact]
    public void OnlyAnActiveInvestorSetsChangeAside()
    {
        var pending = new Investor("buyer-1");
        var setAside = pending.SetAside(orderId: 1, amount: 0.70m);

        Assert.Equal(0m, setAside);
        Assert.Equal(0m, pending.PendingAmount);
        Assert.Empty(pending.Ledger);
    }

    [Fact]
    public void ActiveInvestorAccumulatesSetAsideChange()
    {
        var investor = ActiveInvestor();

        investor.SetAside(1, 0.70m);
        investor.SetAside(2, 0.50m);

        Assert.Equal(1.20m, investor.PendingAmount);
        Assert.Equal(2, investor.Ledger.Count);
        Assert.False(investor.IsReadyToInvest());
    }

    [Fact]
    public void IsReadyToInvestWhenBalanceReachesTheThreshold()
    {
        var investor = ActiveInvestor();
        investor.SetAside(1, Investor.InvestmentThresholdEuros);

        Assert.True(investor.IsReadyToInvest());
    }

    [Fact]
    public void BeginInvestmentMovesTheWholeBalanceAndResetsToZero()
    {
        var investor = ActiveInvestor();
        investor.SetAside(1, 10.50m);

        var investment = investor.BeginInvestment("order-1");

        Assert.Equal(10.50m, investment.Amount);
        Assert.Equal(InvestmentStatus.Pending, investment.Status);
        Assert.Equal(0m, investor.PendingAmount);
        Assert.Single(investor.Investments);
    }

    [Fact]
    public void TotalInvestedCountsPendingAndSettledButNotFailed()
    {
        var investor = ActiveInvestor();

        investor.SetAside(1, 10m);
        var first = investor.BeginInvestment("order-1");
        first.MarkSettled();

        investor.SetAside(2, 10m);
        var second = investor.BeginInvestment("order-2");
        second.MarkFailed();

        investor.SetAside(3, 10m);
        investor.BeginInvestment("order-3"); // stays pending

        Assert.Equal(20m, investor.TotalInvested());
    }
}
