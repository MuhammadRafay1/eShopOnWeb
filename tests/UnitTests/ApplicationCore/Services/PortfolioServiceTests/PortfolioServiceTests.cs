using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.PortfolioServiceTests;

public class PortfolioServiceTests
{
    private readonly IRepository<Investor> _investors = Substitute.For<IRepository<Investor>>();
    private readonly IRepository<ModelPortfolio> _models = Substitute.For<IRepository<ModelPortfolio>>();
    private readonly IRepository<PortfolioPayment> _payments = Substitute.For<IRepository<PortfolioPayment>>();
    private readonly IRepository<Realignment> _realignments = Substitute.For<IRepository<Realignment>>();
    private readonly IUpvestClient _upvest = Substitute.For<IUpvestClient>();
    private readonly IAppLogger<PortfolioService> _logger = Substitute.For<IAppLogger<PortfolioService>>();

    private PortfolioService CreateService() =>
        new(_investors, _models, _payments, _realignments, _upvest, _logger);

    private static InvestorRegistration SampleRegistration() => new(
        "Ada", "Lovelace", "ada@example.com", "1985-12-10", "Munich", "DE", "DE",
        new InvestorAddress("Hauptstrasse 5", "80331", "Munich", "DE"),
        "4930901820", "12345678901", "DE",
        new InvestorIdentityDocument("passport", "C01X00T47", "DE", "2030-01-01"));

    [Fact]
    public async Task RegisterInvestor_CreatesUpvestUser_AndReportsPending()
    {
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByBuyerSpecification>(), Arg.Any<CancellationToken>())
            .Returns((Investor?)null);
        _investors.AddAsync(Arg.Any<Investor>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Investor>());
        _upvest.CreateUserAsync(Arg.Any<InvestorRegistration>(), Arg.Any<CancellationToken>())
            .Returns(new UpvestUser("upvest-user-1", "INACTIVE"));

        var view = await CreateService().RegisterInvestorAsync("buyer-1", SampleRegistration(), CancellationToken.None);

        Assert.Equal("upvest-user-1", view.InvestorId);
        Assert.Equal(PortfolioStatuses.Pending, view.Status);
        await _upvest.Received(1).CreateUserAsync(Arg.Any<InvestorRegistration>(), Arg.Any<CancellationToken>());
        await _investors.Received(1).AddAsync(Arg.Any<Investor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterInvestor_WhenAlreadyRegistered_IsRejected()
    {
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByBuyerSpecification>(), Arg.Any<CancellationToken>())
            .Returns(new Investor("buyer-1", "existing"));

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<PortfolioException>(() =>
            service.RegisterInvestorAsync("buyer-1", SampleRegistration(), CancellationToken.None));
        Assert.Equal(409, ex.StatusCode);
        await _upvest.DidNotReceive().CreateUserAsync(Arg.Any<InvestorRegistration>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PayIn_IsRefused_WhenInvestorNotActive()
    {
        var investor = new Investor("buyer-1", "upvest-user-1");
        investor.SelectModel("balanced");
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByBuyerSpecification>(), Arg.Any<CancellationToken>())
            .Returns(investor);
        // Upvest still has the user as INACTIVE -> not an accepted investor.
        _upvest.GetUserAsync("upvest-user-1", Arg.Any<CancellationToken>())
            .Returns(new UpvestUser("upvest-user-1", "INACTIVE"));

        var ex = await Assert.ThrowsAsync<PortfolioException>(() =>
            CreateService().PayInAsync("buyer-1", 100m, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
        await _upvest.DidNotReceive().CreateTopupAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PayIn_IsRefused_WhenNotAnInvestor()
    {
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByBuyerSpecification>(), Arg.Any<CancellationToken>())
            .Returns((Investor?)null);

        var ex = await Assert.ThrowsAsync<PortfolioException>(() =>
            CreateService().PayInAsync("buyer-1", 100m, CancellationToken.None));
        Assert.Equal(404, ex.StatusCode);
    }
}
