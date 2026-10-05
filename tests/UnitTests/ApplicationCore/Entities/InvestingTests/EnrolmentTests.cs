using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.InvestingTests;

public class EnrolmentTests
{
    private static Enrolment NewEnrolment() => new("demo@example.com", System.Guid.NewGuid());

    [Fact]
    public void StartsPendingWithZeroBalance()
    {
        var e = NewEnrolment();
        Assert.Equal(EnrolmentStatus.Pending, e.Status);
        Assert.Equal(0m, e.PendingAmount);
        Assert.False(e.AccountsProvisioned);
    }

    [Fact]
    public void SetAsideAccumulatesRoundedToCents()
    {
        var e = NewEnrolment();
        e.AddSetAside(0.70m);
        e.AddSetAside(0.50m);
        Assert.Equal(1.20m, e.PendingAmount);
    }

    [Fact]
    public void SetAsideIgnoresNonPositive()
    {
        var e = NewEnrolment();
        e.AddSetAside(0m);
        e.AddSetAside(-1m);
        Assert.Equal(0m, e.PendingAmount);
    }

    [Fact]
    public void TryTakeForInvestmentBelowThresholdTakesNothing()
    {
        var e = NewEnrolment();
        e.AddSetAside(9.99m);
        var taken = e.TryTakeForInvestment(10m, out var amount);
        Assert.False(taken);
        Assert.Equal(0m, amount);
        Assert.Equal(9.99m, e.PendingAmount);
    }

    [Fact]
    public void TryTakeForInvestmentAtThresholdTakesWholeBalanceAndResets()
    {
        var e = NewEnrolment();
        e.AddSetAside(10.40m);
        var taken = e.TryTakeForInvestment(10m, out var amount);
        Assert.True(taken);
        Assert.Equal(10.40m, amount);
        Assert.Equal(0m, e.PendingAmount);
    }

    [Fact]
    public void ReturnToBalanceAddsBack()
    {
        var e = NewEnrolment();
        e.AddSetAside(10m);
        e.TryTakeForInvestment(10m, out _);
        e.ReturnToBalance(10m);
        Assert.Equal(10m, e.PendingAmount);
    }

    [Fact]
    public void RejectedEnrolmentCannotBecomeActive()
    {
        var e = NewEnrolment();
        e.MarkRejected();
        e.MarkActive();
        Assert.Equal(EnrolmentStatus.Rejected, e.Status);
    }

    [Fact]
    public void SetAccountsMarksProvisioned()
    {
        var e = NewEnrolment();
        var group = System.Guid.NewGuid();
        var account = System.Guid.NewGuid();
        e.SetAccounts(group, account);
        Assert.True(e.AccountsProvisioned);
        Assert.Equal(group, e.AccountGroupId);
        Assert.Equal(account, e.AccountId);
    }
}
