using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.InvestingServiceTests;

public class HandlePaidOrderAsyncTests
{
    private const string Shopper = "shopper@example.com";
    private readonly IRepository<Investor> _investors = Substitute.For<IRepository<Investor>>();
    private readonly IUpvestInvestorGateway _gateway = Substitute.For<IUpvestInvestorGateway>();
    private readonly IAppLogger<InvestingService> _logger = Substitute.For<IAppLogger<InvestingService>>();

    private InvestingService CreateService() => new(_investors, _gateway, _logger);

    private void InvestorReturns(Investor? investor) =>
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByShopperIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(investor);

    private static Investor AcceptedInvestor()
    {
        var investor = new Investor(Shopper, Guid.NewGuid());
        investor.MarkAccepted();
        return investor;
    }

    [Fact]
    public async Task NotEnrolled_SetsAsideNothing()
    {
        InvestorReturns(null);

        var setAside = await CreateService().HandlePaidOrderAsync(Shopper, 12.30m, default);

        Assert.Equal(0, setAside);
        await _gateway.DidNotReceive().PlaceInvestmentAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnrolledButNotAccepted_SetsAsideNothing()
    {
        InvestorReturns(new Investor(Shopper, Guid.NewGuid())); // still pending

        var setAside = await CreateService().HandlePaidOrderAsync(Shopper, 12.30m, default);

        Assert.Equal(0, setAside);
    }

    [Fact]
    public async Task Accepted_WholeEuroOrder_SetsAsideNothing()
    {
        var investor = AcceptedInvestor();
        InvestorReturns(investor);

        var setAside = await CreateService().HandlePaidOrderAsync(Shopper, 12.00m, default);

        Assert.Equal(0, setAside);
        Assert.Equal(0, investor.PendingAmountInCents);
    }

    [Fact]
    public async Task Accepted_SetsAsideRoundUp()
    {
        var investor = AcceptedInvestor();
        InvestorReturns(investor);

        var setAside = await CreateService().HandlePaidOrderAsync(Shopper, 12.30m, default);

        Assert.Equal(70, setAside);
        Assert.Equal(70, investor.PendingAmountInCents);
        await _investors.Received().UpdateAsync(investor, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReachingTenEuros_InvestsWholeBalance()
    {
        var investor = AcceptedInvestor();
        investor.SetInvestmentAccount(Guid.NewGuid());
        investor.SetAsideChange(950); // already €9.50 set aside
        InvestorReturns(investor);

        var upvestOrderId = Guid.NewGuid();
        _gateway.PlaceInvestmentAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new UpvestInvestmentPlacement(upvestOrderId, InvestmentStatus.Settled));

        // +€0.70 → €10.20 crosses the €10 threshold.
        var setAside = await CreateService().HandlePaidOrderAsync(Shopper, 12.30m, default);

        Assert.Equal(70, setAside);
        Assert.Equal(0, investor.PendingAmountInCents);           // balance restarts from zero
        await _gateway.Received(1).PlaceInvestmentAsync(Arg.Any<Guid>(), 10.20m, Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        var investment = Assert.Single(investor.Investments);
        Assert.Equal(1020, investment.AmountInCents);
        Assert.Equal(InvestmentStatus.Settled, investment.Status);
        Assert.Equal(upvestOrderId, investment.UpvestOrderId);
    }

    [Fact]
    public async Task BelowTenEuros_DoesNotInvest()
    {
        var investor = AcceptedInvestor();
        InvestorReturns(investor);

        var setAside = await CreateService().HandlePaidOrderAsync(Shopper, 3.20m, default); // €0.80

        Assert.Equal(80, setAside);
        Assert.Equal(80, investor.PendingAmountInCents);
        await _gateway.DidNotReceive().PlaceInvestmentAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvestingFailure_DoesNotPreventSettingAside()
    {
        var investor = AcceptedInvestor();
        investor.SetInvestmentAccount(Guid.NewGuid());
        investor.SetAsideChange(950);
        InvestorReturns(investor);

        _gateway.PlaceInvestmentAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<Task<UpvestInvestmentPlacement>>(_ => throw new Microsoft.eShopWeb.ApplicationCore.Exceptions.UpvestIntegrationException("boom", System.Net.HttpStatusCode.ServiceUnavailable));

        var setAside = await CreateService().HandlePaidOrderAsync(Shopper, 12.30m, default);

        // The round-up is still reported as set aside; the tranche stays pending for reconciliation.
        Assert.Equal(70, setAside);
        var investment = Assert.Single(investor.Investments);
        Assert.Equal(InvestmentStatus.Pending, investment.Status);
    }
}
