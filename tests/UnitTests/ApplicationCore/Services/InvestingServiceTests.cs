using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.ApplicationCore.Services;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services;

public class InvestingServiceTests
{
    private readonly IRepository<Investor> _investors = Substitute.For<IRepository<Investor>>();
    private readonly IRepository<Investment> _investments = Substitute.For<IRepository<Investment>>();
    private readonly IUpvestClient _upvest = Substitute.For<IUpvestClient>();
    private readonly IAppLogger<InvestingService> _logger = Substitute.For<IAppLogger<InvestingService>>();
    private readonly InvestingSettings _settings = new() { InstrumentId = "IE00B4L5Y983", Currency = "EUR", InvestmentThreshold = 10m };

    private InvestingService CreateSut() => new(_investors, _investments, _upvest, _settings, _logger);

    private void InvestorExists(Investor? investor) =>
        _investors.FirstOrDefaultAsync(Arg.Any<ISpecification<Investor>>(), Arg.Any<CancellationToken>())
            .Returns(investor);

    [Fact]
    public async Task NonInvestorSetsNothingAside()
    {
        InvestorExists(null);
        var sut = CreateSut();

        var roundUp = await sut.RecordPaidOrderAsync("buyer-1", 12.30m, CancellationToken.None);

        Assert.Equal(0m, roundUp);
        await _investors.DidNotReceive().UpdateAsync(Arg.Any<Investor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PendingInvestorSetsNothingAside()
    {
        InvestorExists(new Investor("buyer-1")); // still Pending, not accepted
        var sut = CreateSut();

        var roundUp = await sut.RecordPaidOrderAsync("buyer-1", 12.30m, CancellationToken.None);

        Assert.Equal(0m, roundUp);
    }

    [Fact]
    public async Task ActiveInvestorSetsAsideRoundUpToNextEuro()
    {
        var investor = new Investor("buyer-1");
        investor.MarkActive();
        InvestorExists(investor);
        var sut = CreateSut();

        var roundUp = await sut.RecordPaidOrderAsync("buyer-1", 12.30m, CancellationToken.None);

        Assert.Equal(0.70m, roundUp);
        Assert.Equal(0.70m, investor.PendingAmount);
        await _investors.Received().UpdateAsync(investor, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WholeEuroOrderSetsNothingAside()
    {
        var investor = new Investor("buyer-1");
        investor.MarkActive();
        InvestorExists(investor);
        var sut = CreateSut();

        var roundUp = await sut.RecordPaidOrderAsync("buyer-1", 12.00m, CancellationToken.None);

        Assert.Equal(0m, roundUp);
        Assert.Equal(0m, investor.PendingAmount);
    }

    [Fact]
    public async Task BalanceReportsPendingAndSettledInvested()
    {
        var investor = new Investor("buyer-1");
        investor.MarkActive();
        investor.SetAsideChange(3.50m);
        InvestorExists(investor);

        var settled = new Investment("buyer-1", investor.Id, 10m);
        settled.LinkUpvestOrder("order-1");
        settled.MarkSettled();
        var pending = new Investment("buyer-1", investor.Id, 10m); // not counted as invested yet
        _investments.ListAsync(Arg.Any<ISpecification<Investment>>(), Arg.Any<CancellationToken>())
            .Returns(new System.Collections.Generic.List<Investment> { settled, pending });

        var sut = CreateSut();
        var summary = await sut.GetBalanceAsync("buyer-1", CancellationToken.None);

        Assert.Equal(3.50m, summary.PendingAmount);
        Assert.Equal(10m, summary.InvestedAmount);
    }
}
