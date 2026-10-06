using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.InvestorTests;

public class InvestorLedger
{
    [Fact]
    public void StartsPendingWithZeroBalance()
    {
        var investor = new Investor("buyer-1");

        Assert.Equal(EnrolmentStatus.Pending, investor.Status);
        Assert.Equal(0m, investor.PendingAmount);
        Assert.False(investor.IsAcceptedInvestor);
        Assert.False(investor.HasUpvestAccount);
    }

    [Fact]
    public void SetAsideChangeAccumulates()
    {
        var investor = new Investor("buyer-1");

        investor.SetAsideChange(0.70m);
        investor.SetAsideChange(0.50m);

        Assert.Equal(1.20m, investor.PendingAmount);
    }

    [Fact]
    public void SetAsideIgnoresNonPositiveAmounts()
    {
        var investor = new Investor("buyer-1");

        investor.SetAsideChange(0m);
        investor.SetAsideChange(-1m);

        Assert.Equal(0m, investor.PendingAmount);
    }

    [Fact]
    public void ClearPendingResetsBalance()
    {
        var investor = new Investor("buyer-1");
        investor.SetAsideChange(10m);

        investor.ClearPending();

        Assert.Equal(0m, investor.PendingAmount);
    }

    [Fact]
    public void ReturnToPendingAddsBack()
    {
        var investor = new Investor("buyer-1");
        investor.SetAsideChange(3m);
        investor.ClearPending();

        investor.ReturnToPending(10m);

        Assert.Equal(10m, investor.PendingAmount);
    }

    [Fact]
    public void MarkActiveMakesAcceptedInvestor()
    {
        var investor = new Investor("buyer-1");

        investor.MarkActive();

        Assert.Equal(EnrolmentStatus.Active, investor.Status);
        Assert.True(investor.IsAcceptedInvestor);
    }

    [Fact]
    public void LinkUpvestAccountMarksAccountReady()
    {
        var investor = new Investor("buyer-1");

        investor.LinkUpvestAccount("group-1", "account-1");

        Assert.True(investor.HasUpvestAccount);
        Assert.Equal("group-1", investor.UpvestAccountGroupId);
        Assert.Equal("account-1", investor.UpvestAccountId);
    }
}
