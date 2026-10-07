using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.InvestingTests;

public class InvestorTests
{
    private const string BuyerId = "demouser@microsoft.com";
    private const string ProviderUserId = "00000000-0000-0000-0000-000000000001";
    private const string ProviderAccountId = "00000000-0000-0000-0000-000000000002";

    private static Investor AcceptedInvestor()
    {
        var investor = new Investor(BuyerId, ProviderUserId);
        investor.SetProviderAccount(ProviderAccountId);
        investor.MarkActive();
        return investor;
    }

    [Fact]
    public void NewInvestorIsPendingAndCannotInvest()
    {
        var investor = new Investor(BuyerId, ProviderUserId);

        Assert.Equal(EnrolmentStatus.Pending, investor.Status);
        Assert.False(investor.CanInvest);
    }

    [Fact]
    public void SetsNothingAsideUntilAccepted()
    {
        var investor = new Investor(BuyerId, ProviderUserId);
        investor.MarkActive(); // active but no holding account yet

        investor.SetAside(0.70m);

        Assert.Equal(0m, investor.PendingAmount);
    }

    [Fact]
    public void AcceptedInvestorAccruesSetAsideChange()
    {
        var investor = AcceptedInvestor();

        investor.SetAside(0.70m);
        investor.SetAside(0.50m);

        Assert.Equal(1.20m, investor.PendingAmount);
        Assert.False(investor.IsReadyToInvest);
    }

    [Fact]
    public void IsReadyToInvestOnceBalanceReachesTenEuros()
    {
        var investor = AcceptedInvestor();

        for (var i = 0; i < 20; i++)
        {
            investor.SetAside(0.50m);
        }

        Assert.Equal(10.00m, investor.PendingAmount);
        Assert.True(investor.IsReadyToInvest);
    }

    [Fact]
    public void RecordingAnInvestmentResetsPendingAndTracksInvestedTotal()
    {
        var investor = AcceptedInvestor();
        for (var i = 0; i < 20; i++)
        {
            investor.SetAside(0.50m);
        }

        var investment = investor.RecordInvestment("order-123");

        Assert.Equal(0m, investor.PendingAmount);
        Assert.Equal(10.00m, investor.InvestedAmount);
        Assert.Equal(10.00m, investment.Amount);
        Assert.Equal(InvestmentStatus.Pending, investment.Status);
        Assert.Equal("order-123", investor.Investments.Single().ProviderOrderId);
    }

    [Fact]
    public void SettlementReflectsTheOutcomeOnce()
    {
        var investor = AcceptedInvestor();
        investor.SetAside(10m);
        var investment = investor.RecordInvestment("order-123");

        investment.Settle();
        Assert.Equal(InvestmentStatus.Settled, investment.Status);

        // Terminal: a later outcome does not overwrite it.
        investment.Fail();
        Assert.Equal(InvestmentStatus.Settled, investment.Status);
    }
}
