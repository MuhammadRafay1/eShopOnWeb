using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.InvestingAggregate;

public class InvestorTests
{
    private static Investor ActiveInvestorWithAccount()
    {
        var investor = new Investor("buyer-1");
        investor.LinkUpvestUser("user-1");
        investor.MarkActive();
        investor.LinkAccount("group-1", "account-1");
        investor.MarkAccountActive();
        return investor;
    }

    [Fact]
    public void New_investor_is_pending_with_zero_balances()
    {
        var investor = new Investor("buyer-1");
        Assert.Equal(EnrolmentStatus.Pending, investor.Status);
        Assert.Equal(0, investor.PendingAmountCents);
        Assert.Equal(0, investor.InvestedAmountCents);
        Assert.False(investor.CanInvest);
    }

    [Fact]
    public void CanInvest_only_when_active_account_active_and_threshold_reached()
    {
        var investor = ActiveInvestorWithAccount();
        investor.AddSetAside(999);
        Assert.False(investor.CanInvest); // below €10

        investor.AddSetAside(1); // now €10.00
        Assert.True(investor.CanInvest);
    }

    [Fact]
    public void Pending_investor_cannot_invest_even_at_threshold()
    {
        var investor = new Investor("buyer-1");
        investor.LinkUpvestUser("user-1");
        investor.AddSetAside(2000);
        Assert.False(investor.CanInvest);
    }

    [Fact]
    public void TakePendingForInvestment_moves_whole_balance_out()
    {
        var investor = ActiveInvestorWithAccount();
        investor.AddSetAside(1234);
        var taken = investor.TakePendingForInvestment();
        Assert.Equal(1234, taken);
        Assert.Equal(0, investor.PendingAmountCents);
    }

    [Fact]
    public void Settled_investment_adds_to_invested_total()
    {
        var investor = ActiveInvestorWithAccount();
        investor.RecordInvested(1000);
        Assert.Equal(1000, investor.InvestedAmountCents);
    }

    [Fact]
    public void Failed_investment_returns_money_to_pending()
    {
        var investor = ActiveInvestorWithAccount();
        investor.ReturnFailedInvestment(1000);
        Assert.Equal(1000, investor.PendingAmountCents);
        Assert.Equal(0, investor.InvestedAmountCents);
    }
}
