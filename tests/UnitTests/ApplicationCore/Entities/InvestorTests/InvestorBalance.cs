using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.InvestorTests;

public class InvestorBalance
{
    private static Investor NewInvestor() => new("buyer-1");

    [Fact]
    public void StartsPendingWithNoBalanceAndCannotInvest()
    {
        var investor = NewInvestor();

        Assert.Equal(EnrolmentStatus.Pending, investor.Status);
        Assert.Equal(0m, investor.PendingAmount);
        Assert.NotEqual(Guid.Empty, investor.EnrolmentId);
        Assert.False(investor.CanInvest);
    }

    [Fact]
    public void SetAsideAccumulatesRoundedToCents()
    {
        var investor = NewInvestor();

        investor.SetAside(0.70m);
        investor.SetAside(0.50m);

        Assert.Equal(1.20m, investor.PendingAmount);
    }

    [Fact]
    public void DeductPendingLeavesTheRemainder()
    {
        var investor = NewInvestor();
        investor.SetAside(10.50m);

        investor.DeductPending(10.00m);

        Assert.Equal(0.50m, investor.PendingAmount);
    }

    [Fact]
    public void CanInvestOnlyWhenActiveAndProvisioned()
    {
        var investor = NewInvestor();
        investor.SetUpvestUser(Guid.NewGuid(), Guid.NewGuid());

        investor.SetStatus(EnrolmentStatus.Active);
        Assert.False(investor.CanInvest); // no trading account yet

        investor.SetUpvestAccounts(Guid.NewGuid(), Guid.NewGuid());
        Assert.True(investor.CanInvest);
    }
}
