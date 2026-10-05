using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Investing;

public class InvestorTests
{
    private static Investor ActiveInvestor()
    {
        var investor = new Investor("shopper@example.com");
        investor.LinkUpvestUser("user-1");
        investor.MarkActive();
        investor.LinkUpvestAccounts("ag-1", "acct-1");
        return investor;
    }

    [Fact]
    public void New_investor_starts_pending_and_cannot_invest()
    {
        var investor = new Investor("shopper@example.com");
        Assert.Equal(EnrolmentStatus.Pending, investor.Status);
        Assert.False(investor.CanInvest);
        Assert.NotEqual(default, investor.EnrolmentId);
    }

    [Fact]
    public void CanInvest_only_once_active_with_accounts()
    {
        var investor = new Investor("shopper@example.com");
        investor.LinkUpvestUser("user-1");
        Assert.False(investor.CanInvest);
        investor.MarkActive();
        Assert.False(investor.CanInvest); // no accounts yet
        investor.LinkUpvestAccounts("ag-1", "acct-1");
        Assert.True(investor.CanInvest);
    }

    [Fact]
    public void SetAside_accumulates_the_pending_balance()
    {
        var investor = ActiveInvestor();
        investor.SetAside(70);
        investor.SetAside(30);
        Assert.Equal(100, investor.PendingAmountCents);
        Assert.Equal(1.00m, investor.PendingAmount);
    }

    [Fact]
    public void BeginInvestment_moves_the_whole_balance_and_resets_to_zero()
    {
        var investor = ActiveInvestor();
        investor.SetAside(1050);

        var investment = investor.BeginInvestment();

        Assert.Equal(1050, investment.AmountCents);
        Assert.Equal(InvestmentStatus.Pending, investment.Status);
        Assert.Equal(0, investor.PendingAmountCents);
        Assert.Equal(10.50m, investor.InvestedAmount); // in-flight counts as invested
    }

    [Fact]
    public void Failed_investment_is_refunded_and_excluded_from_invested_total()
    {
        var investor = ActiveInvestor();
        investor.SetAside(1000);
        var investment = investor.BeginInvestment();

        investment.MarkFailed();
        investor.RefundToPending(investment);

        Assert.Equal(1000, investor.PendingAmountCents);
        Assert.Equal(0m, investor.InvestedAmount);
        Assert.Equal(InvestmentStatus.Failed, investor.Investments.Single().Status);
    }

    [Fact]
    public void Settled_investment_counts_towards_invested_total()
    {
        var investor = ActiveInvestor();
        investor.SetAside(1000);
        var investment = investor.BeginInvestment();
        investment.MarkSettled();

        Assert.Equal(10.00m, investor.InvestedAmount);
        Assert.Equal(0m, investor.PendingAmount);
    }
}
