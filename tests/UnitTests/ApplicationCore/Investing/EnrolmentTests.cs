using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Investing;

public class EnrolmentTests
{
    private static Enrolment NewEnrolment() => new("buyer-1", "DE", "TAX123");

    [Fact]
    public void New_enrolment_is_pending_and_cannot_invest()
    {
        var e = NewEnrolment();
        Assert.Equal(EnrolmentStatus.Pending, e.Status);
        Assert.False(e.CanInvest);
    }

    [Fact]
    public void Becomes_investable_only_once_active_with_an_account()
    {
        var e = NewEnrolment();
        e.RecordUser("user-1");
        e.RecordAccountGroup("group-1");
        e.RecordAccount("account-1");
        e.Activate();
        Assert.Equal(EnrolmentStatus.Active, e.Status);
        Assert.True(e.CanInvest);
    }

    [Fact]
    public void Activate_does_not_override_a_rejection()
    {
        var e = NewEnrolment();
        e.Reject("declined");
        e.Activate();
        Assert.Equal(EnrolmentStatus.Rejected, e.Status);
    }

    [Fact]
    public void Ledger_sets_aside_invests_and_settles()
    {
        var e = NewEnrolment();
        e.AddSetAside(700);
        e.AddSetAside(400);
        Assert.Equal(1100, e.PendingCents);

        e.BeginInvestment(1100); // the whole balance is invested
        Assert.Equal(0, e.PendingCents);

        e.SettleInvestment(1100);
        Assert.Equal(1100, e.InvestedCents);
        Assert.Equal(0, e.PendingCents);
    }

    [Fact]
    public void A_failed_investment_returns_the_money_to_the_pending_ledger()
    {
        var e = NewEnrolment();
        e.AddSetAside(1000);
        e.BeginInvestment(1000);
        e.FailInvestment(1000);
        Assert.Equal(1000, e.PendingCents);
        Assert.Equal(0, e.InvestedCents);
    }
}
