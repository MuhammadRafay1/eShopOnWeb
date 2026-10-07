using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.InvestingTests;

public class InvestorLedger
{
    private static Investor ActiveInvestor()
    {
        var investor = new Investor("buyer-1");
        investor.LinkUpvestUser("user-1");
        investor.LinkHoldingAccount("group-1", "account-1");
        investor.UpdateStatus(EnrolmentStatus.Active);
        return investor;
    }

    [Fact]
    public void NewInvestorStartsPendingAndCannotInvest()
    {
        var investor = new Investor("buyer-1");
        Assert.Equal(EnrolmentStatus.Pending, investor.Status);
        Assert.False(investor.CanInvest);
        Assert.Equal(0m, investor.PendingAmount);
    }

    [Fact]
    public void AcceptedInvestorWithAccountCanInvest()
    {
        Assert.True(ActiveInvestor().CanInvest);
    }

    [Fact]
    public void ActiveInvestorWithoutAccountCannotInvest()
    {
        var investor = new Investor("buyer-1");
        investor.LinkUpvestUser("user-1");
        investor.UpdateStatus(EnrolmentStatus.Active);
        Assert.False(investor.CanInvest);
    }

    [Fact]
    public void SettingAsideAccumulatesPendingAmount()
    {
        var investor = ActiveInvestor();
        investor.SetAside(0.70m);
        investor.SetAside(0.50m);
        Assert.Equal(1.20m, investor.PendingAmount);
    }

    [Fact]
    public void WithdrawingForInvestmentEmptiesThePendingBalance()
    {
        var investor = ActiveInvestor();
        investor.SetAside(10.00m);

        var amount = investor.WithdrawPendingForInvestment();

        Assert.Equal(10.00m, amount);
        Assert.Equal(0m, investor.PendingAmount);
    }

    [Fact]
    public void ReturningAFailedInvestmentRestoresThePendingBalance()
    {
        var investor = ActiveInvestor();
        investor.SetAside(10.00m);
        var amount = investor.WithdrawPendingForInvestment();

        investor.ReturnToPending(amount);

        Assert.Equal(10.00m, investor.PendingAmount);
    }
}
