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

public class EnrolmentAndSettlementTests
{
    private const string Shopper = "shopper@example.com";
    private readonly IRepository<Investor> _investors = Substitute.For<IRepository<Investor>>();
    private readonly IUpvestInvestorGateway _gateway = Substitute.For<IUpvestInvestorGateway>();
    private readonly IAppLogger<InvestingService> _logger = Substitute.For<IAppLogger<InvestingService>>();

    private InvestingService CreateService() => new(_investors, _gateway, _logger);

    private static EnrolmentForm Form() => new(
        "Ada", "Lovelace", "ada@example.com", new DateOnly(1990, 1, 1), "GB",
        new EnrolmentAddress("1 Test St", "EC1A 1BB", "London", "GB"), "441234567890", "TAX123", "GB");

    [Fact]
    public async Task EnrolAsync_NewShopper_CreatesInvestorFromUpvestIds()
    {
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByShopperIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns((Investor?)null);
        var userId = Guid.NewGuid();
        _gateway.EnrolInvestorAsync(Arg.Any<EnrolmentForm>(), Arg.Any<CancellationToken>())
            .Returns(userId);

        var investor = await CreateService().EnrolAsync(Shopper, Form(), default);

        Assert.Equal(Shopper, investor.ShopperId);
        Assert.Equal(userId, investor.UpvestUserId);
        Assert.Equal(InvestorStatus.Pending, investor.Status);
        await _investors.Received(1).AddAsync(Arg.Any<Investor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnrolAsync_AlreadyEnrolled_ReturnsExistingWithoutCallingUpvest()
    {
        var existing = new Investor(Shopper, Guid.NewGuid());
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByShopperIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(existing);

        var investor = await CreateService().EnrolAsync(Shopper, Form(), default);

        Assert.Same(existing, investor);
        await _gateway.DidNotReceive().EnrolInvestorAsync(Arg.Any<EnrolmentForm>(), Arg.Any<CancellationToken>());
        await _investors.DidNotReceive().AddAsync(Arg.Any<Investor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetEnrolmentAsync_PendingBecomesActive_WhenUpvestAccepts()
    {
        var investor = new Investor(Shopper, Guid.NewGuid()); // pending
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByShopperIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(investor);
        _gateway.GetAcceptanceStatusAsync(investor.UpvestUserId, Arg.Any<CancellationToken>())
            .Returns(InvestorStatus.Active);

        var result = await CreateService().GetEnrolmentAsync(Shopper, default);

        Assert.NotNull(result);
        Assert.Equal(InvestorStatus.Active, result!.Status);
        await _investors.Received().UpdateAsync(investor, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetEnrolmentAsync_NotEnrolled_ReturnsNull()
    {
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByShopperIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns((Investor?)null);

        var result = await CreateService().GetEnrolmentAsync(Shopper, default);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetInvestorAsync_SettlesPendingInvestmentAgainstUpvest()
    {
        var investor = new Investor(Shopper, Guid.NewGuid());
        investor.MarkAccepted();
        investor.SetAsideChange(1000);
        var tranche = investor.BeginInvestment();
        var orderId = Guid.NewGuid();
        tranche.LinkToUpvestOrder(orderId);

        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByShopperIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(investor);
        _gateway.GetInvestmentOutcomeAsync(orderId, Arg.Any<CancellationToken>())
            .Returns(InvestmentStatus.Settled);

        var result = await CreateService().GetInvestorAsync(Shopper, default);

        Assert.NotNull(result);
        Assert.Equal(InvestmentStatus.Settled, result!.Investments.Single().Status);
        Assert.Equal(1000, result.InvestedAmountInCents);
    }
}
