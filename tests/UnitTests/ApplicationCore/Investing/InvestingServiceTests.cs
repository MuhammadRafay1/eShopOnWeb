using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Investing;

public class InvestingServiceTests
{
    private readonly IRepository<Investor> _investors = Substitute.For<IRepository<Investor>>();
    private readonly IUpvestClient _upvest = Substitute.For<IUpvestClient>();
    private readonly IAppLogger<InvestingService> _logger = Substitute.For<IAppLogger<InvestingService>>();
    private readonly InvestingOptions _options = new() { InstrumentId = "IE00B4L5Y983", InvestmentThresholdEuros = 10m };

    private InvestingService CreateService() => new(_investors, _upvest, _options, _logger);

    private static Investor ActiveInvestor()
    {
        var investor = new Investor("shopper@example.com");
        investor.LinkUpvestUser("user-1");
        investor.MarkActive();
        investor.LinkUpvestAccounts("ag-1", "acct-1");
        return investor;
    }

    [Fact]
    public async Task RecordPaidOrder_sets_nothing_aside_when_not_enrolled()
    {
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByShopperIdSpec>(), Arg.Any<CancellationToken>())
            .Returns((Investor?)null);

        var result = await CreateService().RecordPaidOrderAsync("shopper@example.com", 12.30m, default);

        Assert.Equal(0, result);
        await _investors.DidNotReceive().UpdateAsync(Arg.Any<Investor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordPaidOrder_sets_nothing_aside_when_enrolment_still_pending()
    {
        var pending = new Investor("shopper@example.com");
        pending.LinkUpvestUser("user-1"); // still EnrolmentStatus.Pending
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByShopperIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(pending);

        var result = await CreateService().RecordPaidOrderAsync("shopper@example.com", 12.30m, default);

        Assert.Equal(0, result);
        Assert.Equal(0, pending.PendingAmountCents);
    }

    [Fact]
    public async Task RecordPaidOrder_sets_aside_the_round_up_for_an_active_investor()
    {
        var investor = ActiveInvestor();
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByShopperIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(investor);

        var result = await CreateService().RecordPaidOrderAsync("shopper@example.com", 12.30m, default);

        Assert.Equal(70, result);
        Assert.Equal(70, investor.PendingAmountCents);
        await _investors.Received().UpdateAsync(investor, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordPaidOrder_sets_nothing_aside_for_a_whole_euro_order()
    {
        var investor = ActiveInvestor();
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByShopperIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(investor);

        var result = await CreateService().RecordPaidOrderAsync("shopper@example.com", 12.00m, default);

        Assert.Equal(0, result);
        Assert.Equal(0, investor.PendingAmountCents);
        await _investors.DidNotReceive().UpdateAsync(Arg.Any<Investor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Enrol_creates_an_upvest_user_and_returns_the_enrolment()
    {
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByShopperIdSpec>(), Arg.Any<CancellationToken>())
            .Returns((Investor?)null);
        _upvest.CreateUserAsync(Arg.Any<EnrolmentDetails>(), Arg.Any<CancellationToken>())
            .Returns(new UpvestUser("user-1", "INACTIVE"));

        var details = new EnrolmentDetails("Ada", "Lovelace", "ada@example.com", "1985-12-10", "DE",
            new EnrolmentAddress("Rosenweg 221", "45678", "Berlin", "DE"), "+49301234567", "12345678901", "DE");

        var investor = await CreateService().EnrolAsync("shopper@example.com", details, default);

        Assert.Equal("user-1", investor.UpvestUserId);
        Assert.Equal(EnrolmentStatus.Pending, investor.Status); // INACTIVE at Upvest -> pending with us
        await _upvest.Received().CreateUserAsync(Arg.Any<EnrolmentDetails>(), Arg.Any<CancellationToken>());
        await _upvest.Received().CreateKycCheckAsync("user-1", "DE", Arg.Any<CancellationToken>());
        await _investors.Received().AddAsync(Arg.Any<Investor>(), Arg.Any<CancellationToken>());
    }
}
