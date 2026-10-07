using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.InvestingTests;

public class InvestingServiceTests
{
    private readonly IRepository<Investor> _investors = Substitute.For<IRepository<Investor>>();
    private readonly IUpvestInvestingGateway _upvest = Substitute.For<IUpvestInvestingGateway>();
    private readonly IAppLogger<InvestingService> _logger = Substitute.For<IAppLogger<InvestingService>>();

    private InvestingService CreateService() => new(_investors, _upvest, _logger);

    private Investor ActiveInvestorInRepo(string buyerId = "buyer-1")
    {
        var investor = new Investor(buyerId);
        investor.LinkUpvestUser("upvest-user", "upvest-account");
        investor.MarkActive();
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByBuyerIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(investor);
        return investor;
    }

    [Theory]
    [InlineData(12.30, 0.70)]
    [InlineData(9.01, 0.99)]
    [InlineData(5.00, 0.00)]  // already a whole euro
    [InlineData(5.99, 0.01)]
    public async Task SetsAsideTheGapToTheNextWholeEuro(decimal total, decimal expectedRoundUp)
    {
        ActiveInvestorInRepo();
        var service = CreateService();

        var setAside = await service.RecordPaidOrderAsync("buyer-1", orderId: 1, orderTotal: total);

        Assert.Equal(expectedRoundUp, setAside);
    }

    [Fact]
    public async Task SetsNothingAsideWhenShopperHasNotOptedIn()
    {
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByBuyerIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns((Investor?)null);
        var service = CreateService();

        var setAside = await service.RecordPaidOrderAsync("buyer-1", 1, 12.30m);

        Assert.Equal(0m, setAside);
        await _upvest.DidNotReceive().PlaceInvestmentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetsNothingAsideWhenShopperIsNotYetAccepted()
    {
        var pending = new Investor("buyer-1");
        pending.LinkUpvestUser("upvest-user", null);
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByBuyerIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(pending);
        _upvest.RefreshAcceptanceAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new UpvestEnrolmentResult("upvest-user", null, UpvestAcceptanceStatus.Pending));
        var service = CreateService();

        var setAside = await service.RecordPaidOrderAsync("buyer-1", 1, 12.30m);

        Assert.Equal(0m, setAside);
    }

    [Fact]
    public async Task InvestsTheWholeBalanceOnceItReachesTenEuros()
    {
        var investor = ActiveInvestorInRepo();
        investor.SetAside(99, 9.80m); // already accrued
        _upvest.PlaceInvestmentAsync("upvest-user", "upvest-account", Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(ci => new UpvestInvestmentResult("upvest-order-1", "IE00B4L5Y983", UpvestInvestmentState.Settled));
        var service = CreateService();

        var setAside = await service.RecordPaidOrderAsync("buyer-1", 1, orderTotal: 5.50m); // round-up 0.50 -> 10.30

        Assert.Equal(0.50m, setAside);
        await _upvest.Received(1).PlaceInvestmentAsync("upvest-user", "upvest-account", 10.30m, Arg.Any<CancellationToken>());
        Assert.Equal(0m, investor.PendingAmount);                 // balance starts again from zero
        var investment = Assert.Single(investor.Investments);
        Assert.Equal(10.30m, investment.Amount);
        Assert.Equal(InvestmentStatus.Settled, investment.Status);
    }

    [Fact]
    public async Task OrderStillSucceedsWhenInvestingThrows()
    {
        var investor = ActiveInvestorInRepo();
        investor.SetAside(99, 9.80m);
        _upvest.PlaceInvestmentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns<UpvestInvestmentResult>(_ => throw new InvalidOperationException("Upvest is down"));
        var service = CreateService();

        var setAside = await service.RecordPaidOrderAsync("buyer-1", 1, 5.50m);

        Assert.Equal(0.50m, setAside);                 // the round-up is still recorded
        Assert.Equal(10.30m, investor.PendingAmount);  // balance retained for the next attempt
        Assert.Empty(investor.Investments);
    }

    [Fact]
    public async Task BalanceReflectsSettlementReconciledFromUpvest()
    {
        var investor = ActiveInvestorInRepo();
        investor.SetAside(99, 10.00m);
        investor.BeginInvestment("IE00B4L5Y983", "upvest-order-1"); // pending investment
        _upvest.GetInvestmentStateAsync("upvest-order-1", Arg.Any<CancellationToken>())
            .Returns(UpvestInvestmentState.Settled);
        var service = CreateService();

        var balance = await service.GetBalanceAsync("buyer-1");

        Assert.Equal(0m, balance.PendingAmount);
        Assert.Equal(10.00m, balance.InvestedAmount);
        var investment = Assert.Single(investor.Investments);
        Assert.Equal(InvestmentStatus.Settled, investment.Status);
    }

    [Fact]
    public async Task InvestmentsAreReconciledFromUpvest()
    {
        var investor = ActiveInvestorInRepo();
        investor.SetAside(1, 10.00m);
        investor.BeginInvestment("IE00B4L5Y983", "order-A");
        investor.SetAside(2, 11.00m);
        investor.BeginInvestment("IE00B4L5Y983", "order-B");
        _upvest.GetInvestmentStateAsync("order-A", Arg.Any<CancellationToken>()).Returns(UpvestInvestmentState.Settled);
        _upvest.GetInvestmentStateAsync("order-B", Arg.Any<CancellationToken>()).Returns(UpvestInvestmentState.Failed);
        var service = CreateService();

        var investments = await service.GetInvestmentsAsync("buyer-1");

        Assert.Equal(2, investments.Count);
        Assert.Equal(InvestmentStatus.Settled, investments.Single(i => i.Amount == 10.00m).Status);
        Assert.Equal(InvestmentStatus.Failed, investments.Single(i => i.Amount == 11.00m).Status);
    }
}
