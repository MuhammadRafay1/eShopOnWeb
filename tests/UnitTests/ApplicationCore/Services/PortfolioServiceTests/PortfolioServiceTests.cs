using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.PortfolioServiceTests;

public class PortfolioServiceTests
{
    private const string Shopper = "demouser@microsoft.com";

    private readonly IRepository<Investor> _investors = Substitute.For<IRepository<Investor>>();
    private readonly IRepository<ModelPortfolio> _models = Substitute.For<IRepository<ModelPortfolio>>();
    private readonly IRepository<Payment> _payments = Substitute.For<IRepository<Payment>>();
    private readonly IRepository<Realignment> _realignments = Substitute.For<IRepository<Realignment>>();
    private readonly IUpvestInvestmentApi _upvest = Substitute.For<IUpvestInvestmentApi>();
    private readonly IAppLogger<PortfolioService> _logger = Substitute.For<IAppLogger<PortfolioService>>();

    private PortfolioService CreateService() =>
        new(_investors, _models, _payments, _realignments, _upvest, _logger);

    private static InvestorSignUp SampleSignUp() => new(
        "Ada", "Lovelace", "ada@example.com", new DateOnly(1990, 5, 20), "Berlin", "DE", "DE",
        new AddressInput("Rosenweg 221", "10115", "Berlin", "DE"),
        "+49301234567", "12345678901", "DE",
        new IdentityDocumentInput("ID_CARD", "T22000129", "DE", new DateOnly(2032, 1, 1)));

    [Fact]
    public async Task SignUp_WhenUserAndAccountActive_ReportsActive()
    {
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByShopperSpecification>(), Arg.Any<CancellationToken>())
            .Returns((Investor?)null);
        _investors.AddAsync(Arg.Any<Investor>(), Arg.Any<CancellationToken>()).Returns(ci => ci.Arg<Investor>());
        _upvest.CreateUserAsync(Arg.Any<InvestorSignUp>(), Arg.Any<CancellationToken>())
            .Returns(new UpvestUser("user-1", "ACTIVE"));
        _upvest.CreateAccountGroupAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(new UpvestAccountGroup("group-1", "ACTIVE"));
        _upvest.CreateAccountAsync("user-1", "group-1", Arg.Any<CancellationToken>())
            .Returns(new UpvestAccount("account-1", "ACTIVE"));

        var view = await CreateService().SignUpInvestorAsync(Shopper, SampleSignUp());

        Assert.Equal("active", view.Status);
        await _investors.Received().AddAsync(Arg.Is<Investor>(i =>
            i.UpvestUserId == "user-1" && i.UpvestAccountId == "account-1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SignUp_WhenAccountCreationUnauthorised_ReportsPending()
    {
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByShopperSpecification>(), Arg.Any<CancellationToken>())
            .Returns((Investor?)null);
        _investors.AddAsync(Arg.Any<Investor>(), Arg.Any<CancellationToken>()).Returns(ci => ci.Arg<Investor>());
        _upvest.CreateUserAsync(Arg.Any<InvestorSignUp>(), Arg.Any<CancellationToken>())
            .Returns(new UpvestUser("user-1", "INACTIVE"));
        _upvest.CreateAccountGroupAsync("user-1", Arg.Any<CancellationToken>())
            .Throws(new UpvestApiException(401, "unauthorised", "not authorised"));

        var view = await CreateService().SignUpInvestorAsync(Shopper, SampleSignUp());

        Assert.Equal("pending", view.Status);
    }

    [Fact]
    public async Task SignUp_WhenAlreadySignedUp_DoesNotCreateAnotherUser()
    {
        var existing = new Investor(Shopper);
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByShopperSpecification>(), Arg.Any<CancellationToken>())
            .Returns(existing);

        var view = await CreateService().SignUpInvestorAsync(Shopper, SampleSignUp());

        Assert.Equal(existing.PublicId, view.InvestorId);
        await _upvest.DidNotReceive().CreateUserAsync(Arg.Any<InvestorSignUp>(), Arg.Any<CancellationToken>());
        await _investors.DidNotReceive().AddAsync(Arg.Any<Investor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PayIn_WhenNotAccepted_IsRefused()
    {
        var investor = new Investor(Shopper); // pending, no account, no model
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByShopperSpecification>(), Arg.Any<CancellationToken>())
            .Returns(investor);

        var ex = await Assert.ThrowsAsync<PortfolioException>(() =>
            CreateService().PayInAsync(Shopper, 100m));

        Assert.Equal(409, ex.StatusCode);
        await _payments.DidNotReceive().AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PayIn_WhenNotAnInvestor_IsRefused()
    {
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByShopperSpecification>(), Arg.Any<CancellationToken>())
            .Returns((Investor?)null);

        await Assert.ThrowsAsync<PortfolioException>(() => CreateService().PayInAsync(Shopper, 100m));
    }

    [Fact]
    public async Task ChooseModel_WhenModelMissing_ThrowsNotFound()
    {
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByShopperSpecification>(), Arg.Any<CancellationToken>())
            .Returns(new Investor(Shopper));
        _models.FirstOrDefaultAsync(Arg.Any<ModelPortfolioByIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns((ModelPortfolio?)null);

        var ex = await Assert.ThrowsAsync<PortfolioException>(() => CreateService().ChooseModelAsync(Shopper, 5));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task GetInvestor_WhenNoneForShopper_ReturnsNull()
    {
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByShopperSpecification>(), Arg.Any<CancellationToken>())
            .Returns((Investor?)null);

        var view = await CreateService().GetInvestorAsync(Shopper);

        Assert.Null(view);
    }
}
