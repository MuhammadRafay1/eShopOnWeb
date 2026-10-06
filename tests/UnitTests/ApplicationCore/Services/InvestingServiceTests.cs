using System;
using System.Linq;
using System.Threading;
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
    private readonly IRepository<Investor> _repo = Substitute.For<IRepository<Investor>>();
    private readonly IUpvestGateway _gateway = Substitute.For<IUpvestGateway>();
    private readonly IAppLogger<InvestingService> _logger = Substitute.For<IAppLogger<InvestingService>>();
    private readonly InvestingService _service;

    public InvestingServiceTests()
    {
        _service = new InvestingService(_repo, _gateway, _logger);
    }

    private static Investor ActiveInvestor()
    {
        var investor = new Investor("shopper@example.com");
        investor.RecordUpvestUser(Guid.NewGuid(), null);
        investor.RecordUpvestAccount(Guid.NewGuid(), Guid.NewGuid());
        investor.MarkActive();
        return investor;
    }

    private void Returns(Investor? investor) =>
        _repo.FirstOrDefaultAsync(Arg.Any<ISpecification<Investor>>(), Arg.Any<CancellationToken>())
            .Returns(investor);

    [Fact]
    public async Task ApplyPaidOrder_SetsNothingAside_WhenNotEnrolled()
    {
        Returns(null);

        var roundUp = await _service.ApplyPaidOrderAsync("shopper@example.com", 12.30m, CancellationToken.None);

        Assert.Equal(0m, roundUp);
    }

    [Fact]
    public async Task ApplyPaidOrder_SetsNothingAside_WhenPendingNotYetAccepted()
    {
        var pending = new Investor("shopper@example.com");
        pending.RecordUpvestUser(Guid.NewGuid(), null);
        Returns(pending);

        var roundUp = await _service.ApplyPaidOrderAsync("shopper@example.com", 12.30m, CancellationToken.None);

        Assert.Equal(0m, roundUp);
    }

    [Fact]
    public async Task ApplyPaidOrder_SetsAsideRoundUp_ForAcceptedInvestor()
    {
        var investor = ActiveInvestor();
        Returns(investor);

        var roundUp = await _service.ApplyPaidOrderAsync("shopper@example.com", 12.30m, CancellationToken.None);

        Assert.Equal(0.70m, roundUp);
        Assert.Equal(0.70m, investor.SetAsideAmount);
        await _repo.Received().UpdateAsync(investor, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyPaidOrder_InvestsWhenThresholdReached()
    {
        var investor = ActiveInvestor();
        Returns(investor);
        _gateway.PlaceInvestmentAsync(Arg.Any<UpvestInvestmentInstruction>(), Arg.Any<CancellationToken>())
            .Returns(new UpvestInvestmentPlacement
            {
                UpvestOrderId = Guid.NewGuid(),
                InitialStatus = InvestmentStatus.Settled
            });

        // Each order of 9.30 sets aside 0.70; 15 orders -> 10.50, crossing the EUR 10 threshold.
        decimal lastRoundUp = 0m;
        for (var i = 0; i < 15; i++)
        {
            lastRoundUp = await _service.ApplyPaidOrderAsync("shopper@example.com", 9.30m, CancellationToken.None);
        }

        Assert.Equal(0.70m, lastRoundUp);
        await _gateway.Received(1).PlaceInvestmentAsync(
            Arg.Any<UpvestInvestmentInstruction>(), Arg.Any<CancellationToken>());
        var investment = Assert.Single(investor.Investments);
        Assert.Equal(InvestmentStatus.Settled, investment.Status);
        Assert.Equal(0m, investor.SetAsideAmount);
        Assert.Equal(10.50m, investor.InvestedAmount);
    }

    [Fact]
    public async Task ApplyPaidOrder_NeverThrows_WhenInvestingFails()
    {
        var investor = ActiveInvestor();
        Returns(investor);
        _gateway.PlaceInvestmentAsync(Arg.Any<UpvestInvestmentInstruction>(), Arg.Any<CancellationToken>())
            .Returns<UpvestInvestmentPlacement>(_ => throw new UpvestGatewayException("boom"));

        decimal lastRoundUp = 0m;
        var ex = await Record.ExceptionAsync(async () =>
        {
            for (var i = 0; i < 15; i++)
            {
                lastRoundUp = await _service.ApplyPaidOrderAsync("shopper@example.com", 9.30m, CancellationToken.None);
            }
        });

        Assert.Null(ex); // placing the order must never fail because of investing
        Assert.Equal(0.70m, lastRoundUp);
        // A definite failure returns the money to the pending balance; nothing counts as invested.
        var investment = Assert.Single(investor.Investments);
        Assert.Equal(InvestmentStatus.Failed, investment.Status);
        Assert.Equal(0m, investor.InvestedAmount);
        Assert.Equal(10.50m, investor.SetAsideAmount);
    }

    [Fact]
    public async Task Enrol_RegistersAndReturnsPending()
    {
        Returns(null);
        var userId = Guid.NewGuid();
        _gateway.RegisterInvestorAsync(Arg.Any<InvestorEnrolmentData>(), Arg.Any<CancellationToken>())
            .Returns(new UpvestInvestorRegistration { UpvestUserId = userId });

        var investor = await _service.EnrolAsync("shopper@example.com", SampleData(), CancellationToken.None);

        Assert.Equal(EnrolmentStatus.Pending, investor.Status);
        Assert.Equal(userId, investor.UpvestUserId);
        await _repo.Received().AddAsync(Arg.Any<Investor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Enrol_IsIdempotent_WhenAlreadyEnrolled()
    {
        var existing = ActiveInvestor();
        Returns(existing);

        var investor = await _service.EnrolAsync("shopper@example.com", SampleData(), CancellationToken.None);

        Assert.Same(existing, investor);
        await _gateway.DidNotReceive().RegisterInvestorAsync(
            Arg.Any<InvestorEnrolmentData>(), Arg.Any<CancellationToken>());
    }

    private static InvestorEnrolmentData SampleData() => new()
    {
        FirstName = "Ada",
        LastName = "Lovelace",
        Email = "ada@example.com",
        BirthDate = new DateTimeOffset(1985, 12, 10, 0, 0, 0, TimeSpan.Zero),
        Nationality = "DE",
        Address = new EnrolmentAddress { Line1 = "1 Example St", Postcode = "10115", City = "Berlin", Country = "DE" },
        PhoneNumber = "491700000000",
        TaxId = "26954371827",
        TaxCountry = "DE"
    };
}
